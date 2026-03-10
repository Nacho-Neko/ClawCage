using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.Tools.Download;
using ClawCage.WinUI.Services.Tools.Helper;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class InitialDeployPage : Page
    {
        private NodeJsHelper.DetectResult? _localResult;
        private PortableGitHelper.DetectResult? _gitLocalResult;
        private bool _detectionDone;
        private bool _gitDetectionDone;
        private CancellationTokenSource? _installCts;
        private CancellationTokenSource? _gitInstallCts;
        private string _versionRequirementReason = string.Empty;

        public InitialDeployPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = InitializePageAsync();
        }

        private async Task InitializePageAsync()
        {
            await LoadVersionsAsync();
            await DetectEnvironmentAsync();
        }

        // ── Fetch v22.x version list from mirror and populate ComboBox ─────
        private async Task LoadVersionsAsync()
        {
            NodeVersionLoadingRing.Visibility = Visibility.Visible;
            NodeVersionLoadingRing.IsActive = true;
            NodeVersionComboBox.IsEnabled = false;
            NodeVersionComboBox.Items.Clear();
            NodeVersionComboBox.Items.Add(new ComboBoxItem
            {
                Content = "加载中…",
                Tag = string.Empty,
                IsEnabled = false
            });
            NodeVersionComboBox.SelectedIndex = 0;

            try
            {
                var versions = await NodeJsHelper.GetOrderedNodeVersionsAsync();
                NodeVersionComboBox.Items.Clear();
                foreach (var v in versions)
                {
                    NodeVersionComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = v.IsLts ? $"{v.Version}  (LTS)" : v.Version,
                        Tag = v.Version
                    });
                }
                if (NodeVersionComboBox.Items.Count > 0)
                    NodeVersionComboBox.SelectedIndex = 0;
                else
                {
                    NodeVersionComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = "暂无可用版本",
                        Tag = string.Empty,
                        IsEnabled = false
                    });
                    NodeVersionComboBox.SelectedIndex = 0;
                }
            }
            catch
            {
                NodeVersionComboBox.Items.Clear();
                NodeVersionComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "无法获取版本列表（请检查网络）",
                    Tag = string.Empty,
                    IsEnabled = false
                });
                NodeVersionComboBox.SelectedIndex = 0;
            }
            finally
            {
                NodeVersionLoadingRing.IsActive = false;
                NodeVersionLoadingRing.Visibility = Visibility.Collapsed;
                NodeVersionComboBox.IsEnabled = true;
            }
        }

        // ── Initial idle state (no detection run yet) ──────────────────────
        private void ApplyIdleState()
        {
            _detectionDone = false;
            _gitDetectionDone = false;
            NodeDetectRing.Visibility = Visibility.Collapsed;
            NodeStatusIcon.Visibility = Visibility.Collapsed;
            NodeCurrentVersionText.Text = "待检测";
            NodeInstallButton.Visibility = Visibility.Collapsed;
            NodeUninstallButton.Visibility = Visibility.Collapsed;
            GitDetectRing.Visibility = Visibility.Collapsed;
            GitStatusIcon.Visibility = Visibility.Collapsed;
            GitCurrentVersionText.Text = "待检测";
            GitInstallButton.Visibility = Visibility.Collapsed;
            GitUninstallButton.Visibility = Visibility.Collapsed;
            GitDownloadPanel.Visibility = Visibility.Collapsed;
            NodeVersionComboBox.Visibility = Visibility.Visible;
            NodeModeInfoBar.IsOpen = false;
            NpmStatusInfoBar.IsOpen = false;
            ContinueButton.IsEnabled = false;
        }

        // ── Detect local Node.js for selected version ──────────────────────
        private async Task DetectEnvironmentAsync()
        {
            _detectionDone = false;
            _gitDetectionDone = false;
            NodeDetectRing.Visibility = Visibility.Visible;
            NodeDetectRing.IsActive = true;
            NodeStatusIcon.Visibility = Visibility.Collapsed;
            NodeCurrentVersionText.Text = "检测中…";
            NodeInstallButton.Visibility = Visibility.Collapsed;
            NodeUninstallButton.Visibility = Visibility.Collapsed;
            GitDetectRing.Visibility = Visibility.Visible;
            GitDetectRing.IsActive = true;
            GitStatusIcon.Visibility = Visibility.Collapsed;
            GitCurrentVersionText.Text = "检测中…";
            GitInstallButton.Visibility = Visibility.Collapsed;
            GitUninstallButton.Visibility = Visibility.Collapsed;
            GitDownloadPanel.Visibility = Visibility.Collapsed;
            ContinueButton.IsEnabled = false;
            RecheckButton.IsEnabled = false;
            NodeModeInfoBar.IsOpen = false;
            NpmStatusInfoBar.IsOpen = false;

            var databasePath = AppRuntimeState.DatabasePath;
            var selectedVersion = (NodeVersionComboBox.SelectedItem as ComboBoxItem)?.Tag as string;

            string? localExe = !string.IsNullOrEmpty(selectedVersion)
                ? NodeJsHelper.FindNodeExeForVersion(databasePath, selectedVersion)
                : NodeJsHelper.FindLocalNodeExe(databasePath);

            _localResult = localExe is not null
                ? await NodeJsHelper.DetectAtPathAsync(localExe)
                : new NodeJsHelper.DetectResult(false, null, null);
            _detectionDone = true;

            var gitExe = PortableGitHelper.FindLocalGitExe(databasePath);
            _gitLocalResult = gitExe is not null
                ? await PortableGitHelper.DetectAtPathAsync(gitExe)
                : new PortableGitHelper.DetectResult(false, null);

            if (_gitLocalResult.Found)
            {
                await PortableGitHelper.ConfigureDefaultIdentityAsync(databasePath, "mekou", "mekou@mekou.net");
            }

            _gitDetectionDone = true;

            NodeDetectRing.IsActive = false;
            NodeDetectRing.Visibility = Visibility.Collapsed;
            NodeStatusIcon.Visibility = Visibility.Visible;
            GitDetectRing.IsActive = false;
            GitDetectRing.Visibility = Visibility.Collapsed;
            GitStatusIcon.Visibility = Visibility.Visible;
            RecheckButton.IsEnabled = true;

            UpdateNodeUI();
            UpdateGitUI();
            UpdateContinueButtonState();
        }

        // ── Rebuild all row UI from cached results ─────────────────────────
        private void UpdateNodeUI()
        {
            if (!_detectionDone) { ApplyIdleState(); return; }

            bool localOk = _localResult?.Found == true && _localResult.Version is not null;
            var selectedVersionTag = (NodeVersionComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            bool selectedVersionMatched = IsSelectedVersionMatched(selectedVersionTag, _localResult?.Version);
            bool selectedLocalReady = localOk && selectedVersionMatched;

            var result = _localResult;
            bool installed = result?.Found == true && result.Version is not null;
            bool sufficient = NodeJsHelper.IsVersionSufficient(result?.Version);

            // Status icon + version text
            if (!installed)
            {
                NodeCurrentVersionText.Text = "未检测到";
                NodeStatusIcon.Glyph = "\uE711";
                NodeStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (sufficient)
            {
                NodeCurrentVersionText.Text = result!.RawOutput ?? "已安装";
                NodeStatusIcon.Glyph = "\uE73E";
                NodeStatusIcon.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                NodeCurrentVersionText.Text = result!.RawOutput ?? "已安装";
                NodeStatusIcon.Glyph = "\uE7BA";
                NodeStatusIcon.Foreground = new SolidColorBrush(Colors.Orange);
            }

            NodeVersionComboBox.Visibility = Visibility.Visible;
            NodeInstallButton.Visibility = !selectedLocalReady ? Visibility.Visible : Visibility.Collapsed;
            NodeInstallButton.Content = "安装";
            NodeInstallButton.IsEnabled = true;
            NodeUninstallButton.Visibility = selectedLocalReady ? Visibility.Visible : Visibility.Collapsed;
            NodeUninstallButton.IsEnabled = true;

            NpmStatusInfoBar.IsOpen = false;

            if (selectedLocalReady)
                SaveLocalNodePackagePath();

        }

        private void UpdateGitUI()
        {
            if (!_gitDetectionDone)
            {
                GitDetectRing.Visibility = Visibility.Collapsed;
                GitStatusIcon.Visibility = Visibility.Collapsed;
                GitCurrentVersionText.Text = "待检测";
                GitInstallButton.Visibility = Visibility.Collapsed;
                GitUninstallButton.Visibility = Visibility.Collapsed;
                return;
            }

            var gitInstalled = _gitLocalResult?.Found == true;
            if (gitInstalled)
            {
                GitCurrentVersionText.Text = _gitLocalResult?.RawOutput ?? "已安装";
                GitStatusIcon.Glyph = "\uE73E";
                GitStatusIcon.Foreground = new SolidColorBrush(Colors.Green);
                GitInstallButton.Visibility = Visibility.Collapsed;
                GitUninstallButton.Visibility = Visibility.Visible;
                GitUninstallButton.IsEnabled = true;
            }
            else
            {
                GitCurrentVersionText.Text = "未检测到";
                GitStatusIcon.Glyph = "\uE711";
                GitStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                GitInstallButton.Visibility = Visibility.Visible;
                GitInstallButton.IsEnabled = true;
                GitUninstallButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateContinueButtonState()
        {
            var selectedNodeReady = IsSelectedLocalReady();
            var nodeSufficient = NodeJsHelper.IsVersionSufficient(_localResult?.Version);
            var gitReady = IsGitLocalReady();
            ContinueButton.IsEnabled = selectedNodeReady && nodeSufficient && gitReady;
        }

        private static bool IsSelectedVersionMatched(string? selectedVersionTag, Version? detectedVersion)
        {
            if (string.IsNullOrWhiteSpace(selectedVersionTag) || detectedVersion is null)
                return false;

            if (!Version.TryParse(selectedVersionTag.TrimStart('v', 'V'), out var selectedVersion))
                return false;

            return selectedVersion == detectedVersion;
        }

        private void SaveLocalNodePackagePath()
        {
            var databasePath = AppRuntimeState.DatabasePath;
            if (string.IsNullOrWhiteSpace(databasePath)) return;

            var packagePath = Path.Combine(databasePath, ".node_modules");
            try
            {
                Directory.CreateDirectory(packagePath);
            }
            catch
            {
                return;
            }

            var localExe = NodeJsHelper.FindLocalNodeExe(databasePath);
            if (localExe is null) return;

            var nodeDir = Path.GetDirectoryName(localExe);
            if (string.IsNullOrEmpty(nodeDir)) return;
        }

        private async Task RecheckNodeAndUpdateAsync()
        {
            var databasePath = AppRuntimeState.DatabasePath;
            var selectedItem = NodeVersionComboBox.SelectedItem as ComboBoxItem;
            var version = selectedItem?.Tag as string;

            string? nodeExe = !string.IsNullOrEmpty(version)
                ? NodeJsHelper.FindNodeExeForVersion(databasePath, version)
                : NodeJsHelper.FindLocalNodeExe(databasePath);

            _localResult = nodeExe is not null
                ? await NodeJsHelper.DetectAtPathAsync(nodeExe)
                : new NodeJsHelper.DetectResult(false, null, null);

            UpdateNodeUI();
            UpdateContinueButtonState();
        }

        private void NodeVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVersionRequirementHint();
            if (_detectionDone)
                _ = RecheckNodeAndUpdateAsync();
        }

        private void UpdateVersionRequirementHint()
        {
            var selectedItem = NodeVersionComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is not string tagStr || string.IsNullOrEmpty(tagStr))
            {
                VersionRequirementButton.Visibility = Visibility.Collapsed;
                _versionRequirementReason = "未选择目标版本，无法判断版本要求。";
                return;
            }

            bool sufficient = Version.TryParse(tagStr.TrimStart('v'), out var ver) && ver.Major >= 22;
            VersionRequirementButton.Visibility = Visibility.Visible;

            if (sufficient)
            {
                VersionRequirementIcon.Glyph = "\uE73E";
                VersionRequirementIcon.Foreground = new SolidColorBrush(Colors.Green);
                _versionRequirementReason = $"目标版本 {tagStr} 满足最低要求（≥ v22）。";
            }
            else
            {
                VersionRequirementIcon.Glyph = "\uE711";
                VersionRequirementIcon.Foreground = new SolidColorBrush(Colors.Red);
                _versionRequirementReason = $"目标版本 {tagStr} 过低，需 v22 及以上。";
            }

            ToolTipService.SetToolTip(VersionRequirementButton, _versionRequirementReason);
        }

        private async void VersionRequirementButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_versionRequirementReason))
                return;

            var dialog = new ContentDialog
            {
                Title = "版本校验说明",
                Content = _versionRequirementReason,
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void NodeInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var databasePath = AppRuntimeState.DatabasePath;
            var selectedItem = NodeVersionComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is not string version || string.IsNullOrEmpty(version)) return;

            _installCts = new CancellationTokenSource();

            // Switch to download panel
            NodeInstallButton.Visibility = Visibility.Collapsed;
            NodeUninstallButton.Visibility = Visibility.Collapsed;
            NodeVersionComboBox.IsEnabled = false;
            NodeDownloadPanel.Visibility = Visibility.Visible;
            RecheckButton.IsEnabled = false;
            ContinueButton.IsEnabled = false;
            NodeDownloadRing.IsIndeterminate = true;
            NodeDownloadPercentText.Text = string.Empty;
            NodeDownloadSizeText.Text = string.Empty;
            NodeDownloadPhaseText.Text = "准备中…";

            var progress = new Progress<NodeJsDownloader.DownloadProgress>(p =>
            {
                NodeDownloadPhaseText.Text = p.Phase switch
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
                    NodeDownloadRing.IsIndeterminate = false;
                    NodeDownloadRing.Value = pct;
                    NodeDownloadPercentText.Text = $"{(int)pct}%";
                    NodeDownloadSizeText.Text =
                        $"{p.BytesDownloaded / 1_048_576.0:F1} / {p.TotalBytes / 1_048_576.0:F1} MB";
                }
                else
                {
                    NodeDownloadRing.IsIndeterminate = true;
                    NodeDownloadPercentText.Text = p.Phase == NodeJsDownloader.DownloadPhase.Downloading
                        ? $"{p.BytesDownloaded / 1_048_576.0:F1} MB"
                        : string.Empty;
                    NodeDownloadSizeText.Text = string.Empty;
                }
            });

            bool success = false;
            try
            {
                await NodeJsDownloader.DownloadAndInstallAsync(version, databasePath, progress, _installCts.Token);
                success = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                NodeDownloadRing.IsIndeterminate = false;
                NodeDownloadRing.Value = 0;
                NodeDownloadPhaseText.Text = "安装失败";
                NodeDownloadSizeText.Text = ex.Message.Length > 50 ? ex.Message[..50] + "…" : ex.Message;
                await Task.Delay(3000);
            }
            finally
            {
                NodeDownloadPanel.Visibility = Visibility.Collapsed;
                NodeVersionComboBox.IsEnabled = true;
                _installCts?.Dispose();
                _installCts = null;
                RecheckButton.IsEnabled = true;
            }

            if (success)
                await DetectEnvironmentWithRetryAsync();
            else
            {
                UpdateNodeUI();
                UpdateContinueButtonState();
            }
        }

        private async Task DetectEnvironmentWithRetryAsync(int maxAttempts = 3, int delayMs = 800)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                await DetectEnvironmentAsync();
                if (IsSelectedLocalReady())
                    return;

                if (i < maxAttempts - 1)
                    await Task.Delay(delayMs);
            }
        }

        private bool IsSelectedLocalReady()
        {
            var selectedVersionTag = (NodeVersionComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            bool localOk = _localResult?.Found == true && _localResult.Version is not null;
            return localOk
                && IsSelectedVersionMatched(selectedVersionTag, _localResult?.Version);
        }

        private bool IsGitLocalReady() => _gitLocalResult?.Found == true;

        private async void NodeUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var databasePath = AppRuntimeState.DatabasePath;
            var localExe = NodeJsHelper.FindLocalNodeExe(databasePath);
            if (localExe is null) return;

            var versionDir = Path.GetDirectoryName(localExe)!;
            var nodejsDir = Path.Combine(databasePath, NodeJsHelper.NodeJsSubDir);

            // If node.exe lives directly in nodejs\ (flat layout) delete the whole folder;
            // otherwise delete only the version sub-directory (e.g. node-v24.1.0-win-x64).
            var deleteDir = string.Equals(versionDir, nodejsDir, StringComparison.OrdinalIgnoreCase)
                ? nodejsDir
                : versionDir;

            var dirName = Path.GetFileName(deleteDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var confirm = new ContentDialog
            {
                Title = "卸载 Node.js",
                Content = $"确定要删除 {dirName}？此操作不可撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            NodeUninstallButton.IsEnabled = false;
            RecheckButton.IsEnabled = false;

            try
            {
                await Task.Run(() => Directory.Delete(deleteDir, recursive: true));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Length > 100 ? ex.Message[..100] + "…" : ex.Message;
                var errDialog = new ContentDialog
                {
                    Title = "卸载失败",
                    Content = msg,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await errDialog.ShowAsync();
                NodeUninstallButton.IsEnabled = true;
                RecheckButton.IsEnabled = true;
                return;
            }

            await DetectEnvironmentAsync();
        }

        private async void GitInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var databasePath = AppRuntimeState.DatabasePath;
            if (string.IsNullOrWhiteSpace(databasePath))
                return;

            GitInstallButton.IsEnabled = false;
            GitInstallButton.Visibility = Visibility.Collapsed;
            GitUninstallButton.Visibility = Visibility.Collapsed;
            GitDownloadPanel.Visibility = Visibility.Visible;
            RecheckButton.IsEnabled = false;
            ContinueButton.IsEnabled = false;
            NodeVersionComboBox.IsEnabled = false;
            _gitInstallCts = new CancellationTokenSource();

            GitDownloadRing.IsIndeterminate = true;
            GitDownloadRing.Value = 0;
            GitDownloadPercentText.Text = string.Empty;
            GitDownloadSizeText.Text = string.Empty;
            GitDownloadPhaseText.Text = "准备中…";

            var progress = new Progress<PortableGitDownloader.DownloadProgress>(p =>
            {
                GitDownloadPhaseText.Text = p.Phase switch
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
                    GitDownloadRing.IsIndeterminate = false;
                    GitDownloadRing.Value = pct;
                    GitDownloadPercentText.Text = $"{(int)pct}%";
                    GitDownloadSizeText.Text =
                        $"{p.BytesDownloaded / 1_048_576.0:F1} / {p.TotalBytes / 1_048_576.0:F1} MB";
                }
                else
                {
                    GitDownloadRing.IsIndeterminate = true;
                    GitDownloadPercentText.Text = p.Phase == PortableGitDownloader.DownloadPhase.Downloading
                        ? $"{p.BytesDownloaded / 1_048_576.0:F1} MB"
                        : string.Empty;
                    GitDownloadSizeText.Text = string.Empty;
                }
            });

            try
            {
                await PortableGitDownloader.DownloadAndInstallAsync(databasePath, progress, _gitInstallCts.Token);
                await DetectEnvironmentAsync();
            }
            catch (OperationCanceledException)
            {
                UpdateGitUI();
                UpdateContinueButtonState();
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Length > 120 ? ex.Message[..120] + "…" : ex.Message;
                var errDialog = new ContentDialog
                {
                    Title = "安装 PortableGit 失败",
                    Content = msg,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await errDialog.ShowAsync();
            }
            finally
            {
                GitDownloadPanel.Visibility = Visibility.Collapsed;
                NodeVersionComboBox.IsEnabled = true;
                RecheckButton.IsEnabled = true;
                _gitInstallCts?.Dispose();
                _gitInstallCts = null;
            }
        }

        private async void GitUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var databasePath = AppRuntimeState.DatabasePath;
            var gitDir = PortableGitHelper.GetPortableGitDirectory(databasePath);
            if (string.IsNullOrWhiteSpace(databasePath) || !Directory.Exists(gitDir))
                return;

            var confirm = new ContentDialog
            {
                Title = "卸载 PortableGit",
                Content = "确定要删除 PortableGit？此操作不可撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                return;

            GitUninstallButton.IsEnabled = false;
            RecheckButton.IsEnabled = false;

            try
            {
                await Task.Run(() => Directory.Delete(gitDir, recursive: true));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Length > 120 ? ex.Message[..120] + "…" : ex.Message;
                var errDialog = new ContentDialog
                {
                    Title = "卸载 PortableGit 失败",
                    Content = msg,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await errDialog.ShowAsync();
                GitUninstallButton.IsEnabled = true;
                RecheckButton.IsEnabled = true;
                return;
            }

            await DetectEnvironmentAsync();
        }

        private void NodeCancelButton_Click(object sender, RoutedEventArgs e)
        {
            _installCts?.Cancel();
        }

        private void GitCancelButton_Click(object sender, RoutedEventArgs e)
        {
            _gitInstallCts?.Cancel();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SetupPage));
        }

        private async void RecheckButton_Click(object sender, RoutedEventArgs e)
        {
            await DetectEnvironmentAsync();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
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

            Frame.Navigate(typeof(DeployPage));
        }
    }
}

