using ClawCage.WinUI.Model;
using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.OpenClaw;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly OpenClawConfigService _configService = Ioc.Default.GetRequiredService<OpenClawConfigService>();

        private bool _isLoadingRuntimeSettings;
        private PluginsConfig? _pluginsConfig;
        private bool _isUpdating;

        public SettingsPage()
        {
            InitializeComponent();
            Unloaded += SettingsPage_Unloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _configService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
            _configService.ConfigChanged += OpenClawConfigService_ConfigChanged;

            PathPreview.Text = AppRuntimeState.DatabasePath;
            RefreshOpenClawConfigStatus();
            LoadRunModeSettings();
            await LoadPluginsEnabledAsync();
            await RefreshVersionInfoAsync();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _configService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
        }

        private void OpenClawConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(RefreshOpenClawConfigStatus);
        }

        // ── OpenClaw config status ──

        private void RefreshOpenClawConfigStatus()
        {
            var configPath = _configService.GetConfigPath();
            OpenClawConfigPathText.Text = configPath;
            OpenClawConfigStatusText.Text = _configService.IsInitialized()
                ? "状态：已生成（监听中）"
                : "状态：未生成（监听中）";
        }

        // ── Run mode ──

        private void LoadRunModeSettings()
        {
            _isLoadingRuntimeSettings = true;

            var runMode = AppSettings.GetString(AppSettingKeys.RunMode) ?? "gateway";
            RunModeSwitch.IsOn = string.Equals(runMode, "node", StringComparison.OrdinalIgnoreCase);

            HostTextBox.Text = AppSettings.GetString(AppSettingKeys.NodeHost) ?? "127.0.0.1";
            PortTextBox.Text = AppSettings.GetString(AppSettingKeys.NodePort) ?? "18789";

            ApplyNodeModeInputState();
            _isLoadingRuntimeSettings = false;
        }

        private void RunModeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingRuntimeSettings)
                return;

            var runMode = RunModeSwitch.IsOn ? "node" : "gateway";
            AppSettings.SetString(AppSettingKeys.RunMode, runMode);
            ApplyNodeModeInputState();

            RunModeInfoBar.Severity = InfoBarSeverity.Success;
            RunModeInfoBar.Title = RunModeSwitch.IsOn
                ? "已切换为节点模式"
                : "已切换为网关模式";
            RunModeInfoBar.IsOpen = true;
        }

        private void ApplyNodeModeInputState()
        {
            var isNodeMode = RunModeSwitch.IsOn;
            NodeArgsPanel.Visibility = isNodeMode ? Visibility.Visible : Visibility.Collapsed;
            HostTextBox.IsEnabled = isNodeMode;
            PortTextBox.IsEnabled = isNodeMode;
        }

        private void NodeArgs_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!RunModeSwitch.IsOn)
                return;

            var host = HostTextBox.Text.Trim();
            var portText = PortTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(host))
            {
                RunModeInfoBar.Severity = InfoBarSeverity.Warning;
                RunModeInfoBar.Title = "--host 不能为空";
                RunModeInfoBar.IsOpen = true;
                return;
            }

            if (!int.TryParse(portText, out var port) || port is <= 0 or > 65535)
            {
                RunModeInfoBar.Severity = InfoBarSeverity.Warning;
                RunModeInfoBar.Title = "--port 必须是 1-65535 的数字";
                RunModeInfoBar.IsOpen = true;
                return;
            }

            AppSettings.SetString(AppSettingKeys.NodeHost, host);
            AppSettings.SetString(AppSettingKeys.NodePort, port.ToString());

            RunModeInfoBar.Severity = InfoBarSeverity.Success;
            RunModeInfoBar.Title = "节点参数已保存";
            RunModeInfoBar.IsOpen = true;
        }

        // ── Data path ──

        private async void ChangePath_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync().ToTask();
            if (folder is not null)
            {
                AppRuntimeState.SetDatabasePath(folder.Path);
                PathPreview.Text = folder.Path;
                SavedInfoBar.IsOpen = true;
            }
        }

        // ── Plugins enabled ──

        private async Task LoadPluginsEnabledAsync()
        {
            _isLoadingRuntimeSettings = true;

            var result = await _configService.LoadPluginsConfigAsync();
            if (result is not null)
            {
                _pluginsConfig = result.Value.Plugins;
            }
            else
            {
                _pluginsConfig = new PluginsConfig { Enabled = false, Allow = [] };
            }

            PluginsEnabledToggle.IsOn = _pluginsConfig.Enabled;
            _isLoadingRuntimeSettings = false;
        }

        private async void PluginsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingRuntimeSettings || _pluginsConfig is null)
                return;

            _pluginsConfig.Enabled = PluginsEnabledToggle.IsOn;

            try
            {
                var root = await _configService.LoadRootAsync() ?? new JsonObject();
                await _configService.SavePluginsConfigAsync(root, _pluginsConfig);
            }
            catch
            {
                // Silently handle save errors
            }
        }

        // ── Version info ──

        private async Task RefreshVersionInfoAsync()
        {
            CurrentVersionText.Text = "获取中…";
            LatestVersionText.Text = "获取中…";

            var currentTask = OpenClawWatcher.GetVersionAsync();
            var latestTask = OpenClawWatcher.GetLatestVersionAsync();

            var currentResult = await currentTask;
            CurrentVersionText.Text = currentResult.Success && !string.IsNullOrWhiteSpace(currentResult.Output)
                ? currentResult.Output
                : "未知";

            var latestResult = await latestTask;
            LatestVersionText.Text = latestResult.Success && !string.IsNullOrWhiteSpace(latestResult.Output)
                ? latestResult.Output
                : "未知";

            if (currentResult.Success && latestResult.Success
                && !string.IsNullOrWhiteSpace(currentResult.Output)
                && !string.IsNullOrWhiteSpace(latestResult.Output))
            {
                var current = currentResult.Output.TrimStart('v', 'V');
                var latest = latestResult.Output.TrimStart('v', 'V');

                if (Version.TryParse(current, out var currentVer) && Version.TryParse(latest, out var latestVer))
                {
                    if (latestVer > currentVer)
                    {
                        UpdateStatusText.Text = "有新版本可用，点击按钮更新。";
                    }
                    else
                    {
                        UpdateStatusText.Text = "已是最新版本。";
                    }
                }
                else
                {
                    UpdateStatusText.Text = string.Empty;
                }
            }
            else
            {
                UpdateStatusText.Text = string.Empty;
            }
        }

        // ── Update OpenClaw ──

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating)
                return;

            // Check if already on latest version
            var currentText = CurrentVersionText.Text?.Trim() ?? "";
            var latestText = LatestVersionText.Text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(currentText)
                && !string.IsNullOrEmpty(latestText)
                && currentText.Contains(latestText, StringComparison.OrdinalIgnoreCase))
            {
                var confirm = new ContentDialog
                {
                    Title = "版本已是最新",
                    Content = $"当前版本 {CurrentVersionText.Text} 已是最新版本，是否仍要继续更新？",
                    PrimaryButtonText = "继续更新",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            _isUpdating = true;
            UpdateButton.IsEnabled = false;
            UpdateInfoBar.IsOpen = false;
            UpdateStatusText.Text = "正在更新，请在弹出的终端窗口中查看进度…";

            try
            {
                var result = await OpenClawWatcher.UpdateAsync();

                if (result.Success)
                {
                    UpdateInfoBar.Severity = InfoBarSeverity.Success;
                    UpdateInfoBar.Title = "更新完成";
                }
                else
                {
                    UpdateInfoBar.Severity = InfoBarSeverity.Error;
                    UpdateInfoBar.Title = !string.IsNullOrWhiteSpace(result.Error)
                        ? $"更新失败: {result.Error}"
                        : "更新失败";
                }

                UpdateInfoBar.IsOpen = true;
            }
            finally
            {
                _isUpdating = false;
                UpdateButton.IsEnabled = true;
            }

            await RefreshVersionInfoAsync();
        }
    }
}
