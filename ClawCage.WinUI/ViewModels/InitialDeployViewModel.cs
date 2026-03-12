using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.Tools.Download;
using ClawCage.WinUI.Services.Tools.Helper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    public sealed partial class InitialDeployViewModel : ObservableObject
    {
        private NodeJsHelper.DetectResult? _localResult;
        private PortableGitHelper.DetectResult? _gitLocalResult;
        private NodeDependencyDetectResult? _nodeDetection;
        private GitDependencyDetectResult? _gitDetection;
        private CancellationTokenSource? _installCts;
        private CancellationTokenSource? _gitInstallCts;
        private readonly SemaphoreSlim _detectSemaphore = new(1, 1);
        private readonly NodeDependencyRuntimeComponent _nodeComponent = new();
        private readonly GitDependencyRuntimeComponent _gitComponent = new();
        private bool _detectionDone;
        private bool _gitDetectionDone;

        // Navigation actions
        public Action? NavigateToSetup;
        public Action? NavigateToDeploy;
        public Func<string, string, Task>? ShowContentDialog;

        [ObservableProperty] private bool _isNodeVersionLoading;
        [ObservableProperty] private Visibility _nodeVersionLoadingVisibility = Visibility.Collapsed;

        partial void OnIsNodeVersionLoadingChanged(bool value)
        {
            NodeVersionLoadingVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        [ObservableProperty] private bool _isNodeVersionComboEnabled = true;
        [ObservableProperty] private ObservableCollection<NodeVersionItem> _nodeVersions = new();
        [ObservableProperty] private NodeVersionItem? _selectedNodeVersion;

        // Node UI State
        [ObservableProperty] private Visibility _nodeDetectRingVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _nodeStatusIconVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _nodeCurrentVersionText = "待检测";
        [ObservableProperty] private string _nodeStatusGlyph = "\uE711";
        [ObservableProperty] private Brush _nodeStatusForeground = new SolidColorBrush(Colors.Red);
        [ObservableProperty] private Visibility _nodeInstallButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _nodeUninstallButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _nodeDownloadPanelVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _nodeVersionComboBoxVisibility = Visibility.Visible;

        // Node Download State
        [ObservableProperty] private bool _nodeDownloadIndeterminate;
        [ObservableProperty] private double _nodeDownloadValue;
        [ObservableProperty] private string _nodeDownloadPercentText = string.Empty;
        [ObservableProperty] private string _nodeDownloadSizeText = string.Empty;
        [ObservableProperty] private string _nodeDownloadPhaseText = string.Empty;

        // Git UI State
        [ObservableProperty] private Visibility _gitDetectRingVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _gitStatusIconVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _gitCurrentVersionText = "待检测";
        [ObservableProperty] private string _gitStatusGlyph = "\uE711";
        [ObservableProperty] private Brush _gitStatusForeground = new SolidColorBrush(Colors.Red);
        [ObservableProperty] private Visibility _gitInstallButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _gitUninstallButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _gitDownloadPanelVisibility = Visibility.Collapsed;

        // Git Download State
        [ObservableProperty] private bool _gitDownloadIndeterminate;
        [ObservableProperty] private double _gitDownloadValue;
        [ObservableProperty] private string _gitDownloadPercentText = string.Empty;
        [ObservableProperty] private string _gitDownloadSizeText = string.Empty;
        [ObservableProperty] private string _gitDownloadPhaseText = string.Empty;

        // Common
        [ObservableProperty] private bool _isContinueEnabled;
        [ObservableProperty] private bool _isRecheckEnabled = true;

        [ObservableProperty] private Visibility _versionRequirementButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _versionRequirementIconGlyph = "\uE711";
        [ObservableProperty] private Brush _versionRequirementForeground = new SolidColorBrush(Colors.Red);
        [ObservableProperty] private Visibility _dependencyListVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _dependencyPlaceholderVisibility = Visibility.Visible;
        private string _versionRequirementReason = string.Empty;
        public ObservableCollection<DependencyCardItem> DependencyItems { get; } = [];

        public InitialDeployViewModel()
        {
            DependencyItems.CollectionChanged += OnDependencyItemsChanged;
            LoadDependencyItems();
        }

        private void OnDependencyItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateDependencyListVisibility();
        }

        private void LoadDependencyItems()
        {
            DependencyItems.Clear();
            var definitions = new[]
            {
                _nodeComponent.Metadata,
                _gitComponent.Metadata
            };

            foreach (var definition in definitions)
            {
                var iconImage = CreateIconImage(definition.IconResourceUri);
                DependencyItems.Add(new DependencyCardItem
                {
                    Kind = definition.Kind,
                    Name = definition.Name,
                    Description = definition.Description,
                    IconGlyph = definition.IconGlyph,
                    IconImage = iconImage,
                    IconImageVisibility = iconImage is null ? Visibility.Collapsed : Visibility.Visible,
                    IconGlyphVisibility = iconImage is null ? Visibility.Visible : Visibility.Collapsed,
                    TargetVersionText = definition.TargetVersionText,
                    TargetVersionSelectorVisibility = definition.UseTargetVersionSelector ? Visibility.Visible : Visibility.Collapsed,
                    TargetVersionTextVisibility = definition.UseTargetVersionSelector ? Visibility.Collapsed : Visibility.Visible
                });
            }

            UpdateDependencyListVisibility();
        }

        private void UpdateDependencyListVisibility()
        {
            var hasItems = DependencyItems.Count > 0;
            DependencyListVisibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            DependencyPlaceholderVisibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }

        private static BitmapImage? CreateIconImage(string? iconResourceUri)
        {
            if (string.IsNullOrWhiteSpace(iconResourceUri))
                return null;

            try
            {
                return new BitmapImage(new Uri(iconResourceUri));
            }
            catch
            {
                return null;
            }
        }

        private DependencyCardItem? NodeItem => DependencyItems.FirstOrDefault(x => string.Equals(x.Kind, "node", StringComparison.OrdinalIgnoreCase));
        private DependencyCardItem? GitItem => DependencyItems.FirstOrDefault(x => string.Equals(x.Kind, "git", StringComparison.OrdinalIgnoreCase));

        public async Task InitializeAsync()
        {
            await LoadVersionsAsync();
            await DetectEnvironmentAsync();
        }

        [RelayCommand]
        private async Task LoadVersionsAsync()
        {
            IsNodeVersionLoading = true;
            IsNodeVersionComboEnabled = false;
            NodeVersions.Clear();
            NodeVersions.Add(new NodeVersionItem("加载中…", string.Empty, false));
            SelectedNodeVersion = NodeVersions[0];

            try
            {
                var versions = await NodeJsHelper.GetOrderedNodeVersionsAsync();
                NodeVersions.Clear();
                foreach (var v in versions)
                {
                    NodeVersions.Add(new NodeVersionItem(
                        v.IsLts ? $"{v.Version}  (LTS)" : v.Version,
                        v.Version
                    ));
                }

                if (NodeVersions.Count > 0)
                    SelectedNodeVersion = NodeVersions.FirstOrDefault(v => v.Content.Contains("(LTS)", StringComparison.OrdinalIgnoreCase)) ?? NodeVersions[0];
                else
                {
                    NodeVersions.Add(new NodeVersionItem("暂无可用版本", string.Empty, false));
                    SelectedNodeVersion = NodeVersions[0];
                }
            }
            catch
            {
                NodeVersions.Clear();
                NodeVersions.Add(new NodeVersionItem("无法获取版本列表（请检查网络）", string.Empty, false));
                SelectedNodeVersion = NodeVersions[0];
            }
            finally
            {
                IsNodeVersionLoading = false;
                IsNodeVersionComboEnabled = true;
            }
        }

        // Triggered when SelectionChanged in UI
        public void OnNodeVersionSelectionChanged()
        {
            UpdateVersionRequirementHint();
            if (_detectionDone)
                _ = RecheckNodeAndUpdateAsync();
        }

        private void UpdateVersionRequirementHint()
        {
            var tagStr = SelectedNodeVersion?.Tag;
            if (string.IsNullOrEmpty(tagStr))
            {
                VersionRequirementButtonVisibility = Visibility.Collapsed;
                _versionRequirementReason = "未选择目标版本，无法判断版本要求。";
                return;
            }

            bool sufficient = Version.TryParse(tagStr.TrimStart('v'), out var ver) && ver.Major >= 22;
            VersionRequirementButtonVisibility = Visibility.Visible;

            if (sufficient)
            {
                VersionRequirementIconGlyph = "\uE73E";
                VersionRequirementForeground = new SolidColorBrush(Colors.Green);
                _versionRequirementReason = $"目标版本 {tagStr} 满足最低要求（≥ v22）。";
            }
            else
            {
                VersionRequirementIconGlyph = "\uE711";
                VersionRequirementForeground = new SolidColorBrush(Colors.Red);
                _versionRequirementReason = $"目标版本 {tagStr} 过低，需 v22 及以上。";
            }
        }

        [RelayCommand]
        private async Task ShowVersionRequirement()
        {
            if (!string.IsNullOrWhiteSpace(_versionRequirementReason) && ShowContentDialog is not null)
                await ShowContentDialog.Invoke("版本校验说明", _versionRequirementReason);
        }

        [RelayCommand]
        private async Task DetectEnvironmentAsync()
        {
            await DetectEnvironmentWithUiRefreshAsync();
        }

        private async Task DetectEnvironmentWithUiRefreshAsync()
        {
            await _detectSemaphore.WaitAsync();
            try
            {
                try
                {
                    await DetectEnvironmentCoreAsync();
                }
                catch (Exception ex)
                {
                    _nodeDetection ??= new NodeDependencyDetectResult(new NodeJsHelper.DetectResult(false, null, null), false, false, false, "未检测到");
                    _gitDetection ??= new GitDependencyDetectResult(new PortableGitHelper.DetectResult(false, null), false, "未检测到");
                    if (ShowContentDialog is not null)
                        await ShowContentDialog.Invoke("环境检测失败", ex.Message);
                }
                RefreshEnvironmentUi();
            }
            finally
            {
                _detectSemaphore.Release();
            }
        }

        private async Task DetectEnvironmentCoreAsync()
        {
            _detectionDone = false;
            _gitDetectionDone = false;

            var nodeItem = NodeItem;
            var gitItem = GitItem;

            if (nodeItem is not null)
            {
                nodeItem.DetectRingVisibility = Visibility.Visible;
                nodeItem.StatusIconVisibility = Visibility.Collapsed;
                nodeItem.CurrentVersionText = "检测中…";
                nodeItem.InstallButtonVisibility = Visibility.Collapsed;
                nodeItem.UninstallButtonVisibility = Visibility.Collapsed;
            }

            if (gitItem is not null)
            {
                gitItem.DetectRingVisibility = Visibility.Visible;
                gitItem.StatusIconVisibility = Visibility.Collapsed;
                gitItem.CurrentVersionText = "检测中…";
                gitItem.InstallButtonVisibility = Visibility.Collapsed;
                gitItem.UninstallButtonVisibility = Visibility.Collapsed;
                gitItem.DownloadPanelVisibility = Visibility.Collapsed;
            }

            IsContinueEnabled = false;
            IsRecheckEnabled = false;

            var databasePath = AppRuntimeState.DatabasePath;
            var selectedVersion = SelectedNodeVersion?.Tag;

            _nodeDetection = await _nodeComponent.DetectAsync(databasePath, selectedVersion);
            _localResult = _nodeDetection.LocalResult;
            _detectionDone = true;

            _gitDetection = await _gitComponent.DetectAsync(databasePath);
            _gitLocalResult = _gitDetection.LocalResult;

            _gitDetectionDone = true;

            if (nodeItem is not null)
            {
                nodeItem.DetectRingVisibility = Visibility.Collapsed;
                nodeItem.StatusIconVisibility = Visibility.Visible;
            }

            if (gitItem is not null)
            {
                gitItem.DetectRingVisibility = Visibility.Collapsed;
                gitItem.StatusIconVisibility = Visibility.Visible;
            }

            IsRecheckEnabled = true;
        }

        private void RefreshEnvironmentUi()
        {
            UpdateNodeUI();
            UpdateGitUI();
            UpdateContinueButtonState();
        }

        private async Task RecheckNodeAndUpdateAsync()
        {
            await _detectSemaphore.WaitAsync();
            try
            {
                var nodeItem = NodeItem;
                if (nodeItem is not null)
                {
                    nodeItem.DetectRingVisibility = Visibility.Visible;
                    nodeItem.StatusIconVisibility = Visibility.Collapsed;
                    nodeItem.CurrentVersionText = "检测中…";
                    nodeItem.InstallButtonVisibility = Visibility.Collapsed;
                    nodeItem.UninstallButtonVisibility = Visibility.Collapsed;
                }
                IsRecheckEnabled = false;

                var databasePath = AppRuntimeState.DatabasePath;
                var version = SelectedNodeVersion?.Tag;

                try
                {
                    _nodeDetection = await _nodeComponent.DetectAsync(databasePath, version);
                    _localResult = _nodeDetection.LocalResult;
                }
                catch
                {
                    _nodeDetection = new NodeDependencyDetectResult(new NodeJsHelper.DetectResult(false, null, null), false, false, false, "未检测到");
                    _localResult = _nodeDetection.LocalResult;
                }

                _detectionDone = true;
                if (nodeItem is not null)
                {
                    nodeItem.DetectRingVisibility = Visibility.Collapsed;
                    nodeItem.StatusIconVisibility = Visibility.Visible;
                }
                IsRecheckEnabled = true;

                UpdateNodeUI();
                UpdateContinueButtonState();
            }
            finally
            {
                _detectSemaphore.Release();
            }
        }

        private async Task RecheckGitAndUpdateAsync()
        {
            await _detectSemaphore.WaitAsync();
            try
            {
                var gitItem = GitItem;
                if (gitItem is not null)
                {
                    gitItem.DetectRingVisibility = Visibility.Visible;
                    gitItem.StatusIconVisibility = Visibility.Collapsed;
                    gitItem.CurrentVersionText = "检测中…";
                    gitItem.InstallButtonVisibility = Visibility.Collapsed;
                    gitItem.UninstallButtonVisibility = Visibility.Collapsed;
                }
                IsRecheckEnabled = false;

                var databasePath = AppRuntimeState.DatabasePath;
                try
                {
                    _gitDetection = await _gitComponent.DetectAsync(databasePath);
                    _gitLocalResult = _gitDetection.LocalResult;
                }
                catch
                {
                    _gitDetection = new GitDependencyDetectResult(new PortableGitHelper.DetectResult(false, null), false, "未检测到");
                    _gitLocalResult = _gitDetection.LocalResult;
                }

                _gitDetectionDone = true;
                if (gitItem is not null)
                {
                    gitItem.DetectRingVisibility = Visibility.Collapsed;
                    gitItem.StatusIconVisibility = Visibility.Visible;
                }
                IsRecheckEnabled = true;

                UpdateGitUI();
                UpdateContinueButtonState();
            }
            finally
            {
                _detectSemaphore.Release();
            }
        }

        private void UpdateNodeUI()
        {
            var nodeItem = NodeItem;
            if (nodeItem is null)
                return;

            var detection = _nodeDetection;
            bool selectedLocalReady = detection?.SelectedReady == true;
            bool installed = detection?.Installed == true;
            bool sufficient = detection?.VersionSufficient == true;

            if (!installed)
            {
                nodeItem.CurrentVersionText = "未检测到";
                nodeItem.StatusGlyph = "\uE711";
                nodeItem.StatusForeground = new SolidColorBrush(Colors.Red);
            }
            else if (sufficient)
            {
                nodeItem.CurrentVersionText = detection?.CurrentVersion ?? "已安装";
                nodeItem.StatusGlyph = "\uE73E";
                nodeItem.StatusForeground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                nodeItem.CurrentVersionText = detection?.CurrentVersion ?? "已安装";
                nodeItem.StatusGlyph = "\uE7BA";
                nodeItem.StatusForeground = new SolidColorBrush(Colors.Orange);
            }

            nodeItem.InstallButtonVisibility = !selectedLocalReady ? Visibility.Visible : Visibility.Collapsed;
            nodeItem.UninstallButtonVisibility = selectedLocalReady ? Visibility.Visible : Visibility.Collapsed;

            // NpmStatusInfoBar logic was in code-behind but not fully implemented/used in snippets

            if (selectedLocalReady)
                SaveLocalNodePackagePath();
        }

        private void UpdateGitUI()
        {
            var gitItem = GitItem;
            if (gitItem is null)
                return;

            var detection = _gitDetection;
            var gitInstalled = detection?.Installed == true;
            if (gitInstalled)
            {
                gitItem.CurrentVersionText = detection?.CurrentVersion ?? "已安装";
                gitItem.StatusGlyph = "\uE73E";
                gitItem.StatusForeground = new SolidColorBrush(Colors.Green);
                gitItem.InstallButtonVisibility = Visibility.Collapsed;
                gitItem.UninstallButtonVisibility = Visibility.Visible;
            }
            else
            {
                gitItem.CurrentVersionText = "未检测到";
                gitItem.StatusGlyph = "\uE711";
                gitItem.StatusForeground = new SolidColorBrush(Colors.Red);
                gitItem.InstallButtonVisibility = Visibility.Visible;
                gitItem.UninstallButtonVisibility = Visibility.Collapsed;
            }
        }

        private void UpdateContinueButtonState()
        {
            var selectedNodeReady = IsSelectedLocalReady();
            var nodeSufficient = _nodeDetection?.VersionSufficient == true;
            var gitReady = IsGitLocalReady();
            IsContinueEnabled = selectedNodeReady && nodeSufficient && gitReady;
        }

        private bool IsSelectedLocalReady()
        {
            return _nodeDetection?.SelectedReady == true;
        }

        private bool IsGitLocalReady() => _gitDetection?.Installed == true;

        private void SaveLocalNodePackagePath()
        {
            var databasePath = AppRuntimeState.DatabasePath;
            if (string.IsNullOrWhiteSpace(databasePath)) return;

            var packagePath = Path.Combine(databasePath, ".node_modules");
            try
            {
                Directory.CreateDirectory(packagePath);
            }
            catch { return; }

            var localExe = NodeJsHelper.FindLocalNodeExe(databasePath);
            if (localExe is null) return;
        }

        [RelayCommand]
        private async Task InstallNodeAsync()
        {
            var nodeItem = NodeItem;
            if (nodeItem is null)
                return;

            var databasePath = AppRuntimeState.DatabasePath;
            var version = SelectedNodeVersion?.Tag;
            if (string.IsNullOrEmpty(version)) return;

            _installCts = new CancellationTokenSource();

            nodeItem.InstallButtonVisibility = Visibility.Collapsed;
            nodeItem.UninstallButtonVisibility = Visibility.Collapsed;
            IsNodeVersionComboEnabled = false;
            nodeItem.DownloadPanelVisibility = Visibility.Visible;
            IsRecheckEnabled = false;
            IsContinueEnabled = false;
            nodeItem.DownloadIndeterminate = true;
            nodeItem.DownloadPercentText = string.Empty;
            nodeItem.DownloadSizeText = string.Empty;
            nodeItem.DownloadPhaseText = "准备中…";

            var progress = new Progress<NodeJsDownloader.DownloadProgress>(p =>
            {
                nodeItem.DownloadPhaseText = p.Phase switch
                {
                    NodeJsDownloader.DownloadPhase.Preparing => "连接服务器…",
                    NodeJsDownloader.DownloadPhase.Downloading => "下载中…",
                    NodeJsDownloader.DownloadPhase.Extracting => "解压中…",
                    NodeJsDownloader.DownloadPhase.Installing => "安装中…",
                    NodeJsDownloader.DownloadPhase.Completed => "完成",
                    _ => string.Empty
                };

                if (p.Phase == NodeJsDownloader.DownloadPhase.Downloading && p.TotalBytes > 0)
                {
                    double pct = (double)p.BytesDownloaded / p.TotalBytes * 100.0;
                    nodeItem.DownloadIndeterminate = false;
                    nodeItem.DownloadValue = pct;
                    nodeItem.DownloadPercentText = $"{(int)pct}%";
                    nodeItem.DownloadSizeText = $"{p.BytesDownloaded / 1_048_576.0:F1} / {p.TotalBytes / 1_048_576.0:F1} MB";
                }
                else
                {
                    nodeItem.DownloadIndeterminate = true;
                    nodeItem.DownloadPercentText = p.Phase == NodeJsDownloader.DownloadPhase.Downloading
                        ? $"{p.BytesDownloaded / 1_048_576.0:F1} MB"
                        : string.Empty;
                    nodeItem.DownloadSizeText = string.Empty;
                }
            });

            bool success = false;
            try
            {
                var result = await _nodeComponent.InstallAsync(new NodeDependencyInstallRequest(
                    databasePath,
                    version,
                    progress,
                    _installCts.Token));

                _nodeDetection = result.Detection;
                _localResult = result.Detection.LocalResult;
                success = result.Success;

                if (!result.Success && !result.Cancelled)
                {
                    nodeItem.DownloadIndeterminate = false;
                    nodeItem.DownloadValue = 0;
                    nodeItem.DownloadPhaseText = "安装失败";
                    var msg = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "安装失败" : result.ErrorMessage;
                    nodeItem.DownloadSizeText = msg.Length > 50 ? msg[..50] + "…" : msg;
                    await Task.Delay(3000);
                }
            }
            finally
            {
                nodeItem.DownloadPanelVisibility = Visibility.Collapsed;
                IsNodeVersionComboEnabled = true;
                _installCts?.Dispose();
                _installCts = null;
                IsRecheckEnabled = true;
            }

            if (success)
            {
                UpdateNodeUI();
                UpdateContinueButtonState();
            }
            else
            {
                UpdateNodeUI();
                UpdateContinueButtonState();
            }
        }

        [RelayCommand]
        private void CancelNodeInstall()
        {
            _installCts?.Cancel();
        }

        [RelayCommand]
        private async Task UninstallNodeAsync()
        {
            var nodeItem = NodeItem;
            if (nodeItem is null)
                return;

            var databasePath = AppRuntimeState.DatabasePath;
            nodeItem.UninstallButtonVisibility = Visibility.Collapsed;
            IsRecheckEnabled = false;

            var result = await _nodeComponent.UninstallAsync(
                new NodeDependencyUninstallRequest(databasePath, SelectedNodeVersion?.Tag, RequestDeleteConfirmation));

            _nodeDetection = result.Detection;
            _localResult = result.Detection.LocalResult;

            if (!result.Success && !result.Cancelled && ShowContentDialog is not null)
                await ShowContentDialog.Invoke("卸载失败", result.ErrorMessage ?? "卸载失败。");

            UpdateNodeUI();
            UpdateContinueButtonState();
            IsRecheckEnabled = true;
        }

        public Func<string, string, Task<bool>>? RequestDeleteConfirmation;

        [RelayCommand]
        private async Task InstallGitAsync()
        {
            var gitItem = GitItem;
            if (gitItem is null)
                return;

            var databasePath = AppRuntimeState.DatabasePath;
            if (string.IsNullOrWhiteSpace(databasePath)) return;

            gitItem.InstallButtonVisibility = Visibility.Collapsed;
            gitItem.UninstallButtonVisibility = Visibility.Collapsed;
            gitItem.DownloadPanelVisibility = Visibility.Visible;
            IsRecheckEnabled = false;
            IsContinueEnabled = false;
            IsNodeVersionComboEnabled = false;

            _gitInstallCts = new CancellationTokenSource();
            gitItem.DownloadIndeterminate = true;
            gitItem.DownloadValue = 0;
            gitItem.DownloadPercentText = string.Empty;
            gitItem.DownloadSizeText = string.Empty;
            gitItem.DownloadPhaseText = "准备中…";

            var progress = new Progress<PortableGitDownloader.DownloadProgress>(p =>
            {
                gitItem.DownloadPhaseText = p.Phase switch
                {
                    PortableGitDownloader.DownloadPhase.Preparing => "连接服务器…",
                    PortableGitDownloader.DownloadPhase.Downloading => "下载中…",
                    PortableGitDownloader.DownloadPhase.Extracting => "解压中…",
                    PortableGitDownloader.DownloadPhase.Installing => "安装中…",
                    PortableGitDownloader.DownloadPhase.Completed => "完成",
                    _ => string.Empty
                };

                if (p.Phase == PortableGitDownloader.DownloadPhase.Downloading && p.TotalBytes > 0)
                {
                    double pct = (double)p.BytesDownloaded / p.TotalBytes * 100.0;
                    gitItem.DownloadIndeterminate = false;
                    gitItem.DownloadValue = pct;
                    gitItem.DownloadPercentText = $"{(int)pct}%";
                    gitItem.DownloadSizeText = $"{p.BytesDownloaded / 1_048_576.0:F1} / {p.TotalBytes / 1_048_576.0:F1} MB";
                }
                else
                {
                    gitItem.DownloadIndeterminate = true;
                    gitItem.DownloadPercentText = p.Phase == PortableGitDownloader.DownloadPhase.Downloading
                        ? $"{p.BytesDownloaded / 1_048_576.0:F1} MB"
                        : string.Empty;
                    gitItem.DownloadSizeText = string.Empty;
                }
            });

            try
            {
                var result = await _gitComponent.InstallAsync(new GitDependencyInstallRequest(
                    databasePath,
                    progress,
                    _gitInstallCts.Token));

                _gitDetection = result.Detection;
                _gitLocalResult = result.Detection.LocalResult;

                if (!result.Success && !result.Cancelled && ShowContentDialog is not null)
                    await ShowContentDialog.Invoke("安装 PortableGit 失败", result.ErrorMessage ?? "安装失败。");

                UpdateGitUI();
                UpdateContinueButtonState();
            }
            finally
            {
                gitItem.DownloadPanelVisibility = Visibility.Collapsed;
                IsNodeVersionComboEnabled = true;
                IsRecheckEnabled = true;
                _gitInstallCts?.Dispose();
                _gitInstallCts = null;
            }
        }

        [RelayCommand]
        private void CancelGitInstall()
        {
            _gitInstallCts?.Cancel();
        }

        [RelayCommand]
        private async Task DependencyInstallAsync(string? kind)
        {
            if (string.Equals(kind, _nodeComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                await InstallNodeAsync();
            else if (string.Equals(kind, _gitComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                await InstallGitAsync();
        }

        [RelayCommand]
        private async Task DependencyUninstallAsync(string? kind)
        {
            if (string.Equals(kind, _nodeComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                await UninstallNodeAsync();
            else if (string.Equals(kind, _gitComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                await UninstallGitAsync();
        }

        [RelayCommand]
        private void DependencyCancel(string? kind)
        {
            if (string.Equals(kind, _nodeComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                CancelNodeInstall();
            else if (string.Equals(kind, _gitComponent.Metadata.Kind, StringComparison.OrdinalIgnoreCase))
                CancelGitInstall();
        }

        [RelayCommand]
        private async Task UninstallGitAsync()
        {
            var gitItem = GitItem;
            if (gitItem is null)
                return;

            var databasePath = AppRuntimeState.DatabasePath;
            gitItem.UninstallButtonVisibility = Visibility.Collapsed;
            IsRecheckEnabled = false;

            var result = await _gitComponent.UninstallAsync(new GitDependencyUninstallRequest(databasePath, RequestDeleteConfirmation));
            _gitDetection = result.Detection;
            _gitLocalResult = result.Detection.LocalResult;

            if (!result.Success && !result.Cancelled && ShowContentDialog is not null)
                await ShowContentDialog.Invoke("卸载 PortableGit 失败", result.ErrorMessage ?? "卸载失败。");

            UpdateGitUI();
            UpdateContinueButtonState();
            IsRecheckEnabled = true;
        }

        [RelayCommand]
        private void Back()
        {
            NavigateToSetup?.Invoke();
        }

        [RelayCommand]
        private void Continue()
        {
            var databasePath = AppRuntimeState.DatabasePath;

            if (_localResult?.Found == true)
            {
                var localExe = NodeJsHelper.FindLocalNodeExe(databasePath);
                if (localExe is not null)
                {
                    var nodeDir = Path.GetDirectoryName(localExe)!;
                    SecureConfigStore.AddPathEntry(databasePath, nodeDir);
                    SecureConfigStore.SetEnvironmentValue("NODE_DIR", nodeDir);
                }
            }
            else
            {
                SecureConfigStore.RemoveEnvironmentValue(databasePath, "NODE_DIR");
            }

            if (_gitLocalResult?.Found == true)
            {
                var gitExe = PortableGitHelper.FindLocalGitExe(databasePath);
                if (gitExe is not null)
                {
                    var gitDir = Path.GetDirectoryName(Path.GetDirectoryName(gitExe) ?? string.Empty);
                    var gitCmdDir = Path.GetDirectoryName(gitExe);
                    if (!string.IsNullOrWhiteSpace(gitCmdDir))
                        SecureConfigStore.AddPathEntry(databasePath, gitCmdDir);
                    if (!string.IsNullOrWhiteSpace(gitDir))
                        SecureConfigStore.SetEnvironmentValue("GIT_DIR", gitDir);
                }
            }
            else
            {
                SecureConfigStore.RemoveEnvironmentValue(databasePath, "GIT_DIR");
            }

            NavigateToDeploy?.Invoke();
        }
    }

    public class NodeVersionItem
    {
        public string Content { get; }
        public string Tag { get; }
        public bool IsEnabled { get; }

        public NodeVersionItem(string content, string tag, bool isEnabled = true)
        {
            Content = content;
            Tag = tag;
            IsEnabled = isEnabled;
        }

        public override string ToString() => Content;
    }
}
