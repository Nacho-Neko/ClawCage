using ClawCage.WinUI.Components;
using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.OpenClaw;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class OverviewPage : Page
    {
        private bool _isInitialized;
        private bool _isRunning;
        private bool _isBusy;
        private string? _consoleUrl;

        public OverviewPage()
        {
            InitializeComponent();
            Loaded += OverviewPage_Loaded;
            Unloaded += OverviewPage_Unloaded;
        }

        private async void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
            OpenClawConfigService.ConfigChanged += OpenClawConfigService_ConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OpenClawWatcher_RunningStateChanged;
            OpenClawWatcher.RunningStateChanged += OpenClawWatcher_RunningStateChanged;
            await RefreshStateAsync();
        }

        private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OpenClawWatcher_RunningStateChanged;
        }

        private void OpenClawConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(async () => await RefreshStateAsync());
        }

        private void OpenClawWatcher_RunningStateChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(async () => await RefreshStateAsync());
        }

        private async Task RefreshStateAsync()
        {
            _isInitialized = OpenClawConfigService.IsInitialized();
            _isRunning = _isInitialized && OpenClawWatcher.IsRunning;
            _consoleUrl = await OpenClawConfigService.TryGetConsoleUrlAsync();

            UpdateVisualState();
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (!_isInitialized)
            {
                await InitializeAsync();
                return;
            }

            if (_isRunning)
                await StopAsync();
            else
                await RunAsync();
        }

        private async Task InitializeAsync()
        {
            var modeConfigured = await OpenClawInitModeDialog.ShowAndSaveAsync(XamlRoot);
            if (!modeConfigured)
                return;

            SetBusy(true, "初始化中...");
            var result = await OpenClawWatcher.InitializeAsync();
            /*
            if (!result.Success)
            {
                SetBusy(false);
                await ShowDialogAsync("初始化失败", string.IsNullOrWhiteSpace(result.Error) ? "初始化失败。" : result.Error);
                return;
            }
            */
            await Task.Delay(500);
            _isInitialized = OpenClawConfigService.IsInitialized();

            SetBusy(false);
            if (!_isInitialized)
            {
                await ShowDialogAsync("初始化未完成", $"未检测到配置文件: {OpenClawConfigService.GetConfigPath()}");
            }

            await RefreshStateAsync();
        }

        private async Task RunAsync()
        {
            SetBusy(true, "启动中...");
            var result = await OpenClawWatcher.StartAsync();
            SetBusy(false);
            if (!result.Success)
            {
                await ShowDialogAsync("启动失败", string.IsNullOrWhiteSpace(result.Error) ? "启动失败。" : result.Error);
                return;
            }

            await RefreshStateAsync();
        }

        private async Task StopAsync()
        {
            SetBusy(true, "停止中...");
            var result = await OpenClawWatcher.StopAsync();
            SetBusy(false);
            if (!result.Success)
            {
                await ShowDialogAsync("停止失败", string.IsNullOrWhiteSpace(result.Error) ? "停止失败。" : result.Error);
                return;
            }

            await RefreshStateAsync();
        }

        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || !_isInitialized)
                return;

            SetBusy(true, "重启中...");
            var result = await OpenClawWatcher.RestartAsync(AppRuntimeState.DatabasePath);
            SetBusy(false);
            if (!result.Success)
            {
                await ShowDialogAsync("重启失败", string.IsNullOrWhiteSpace(result.Error) ? "重启失败。" : result.Error);
                return;
            }

            await RefreshStateAsync();
        }

        private async void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var configPath = OpenClawConfigService.GetConfigPath();
            await ShowDialogAsync("帮助", $"首次初始化会生成:\n{configPath}\n\n请在打开的 OpenClaw 控制台中查看详细日志。\n");
        }

        private async void OpenConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_consoleUrl))
            {
                await ShowDialogAsync("无法打开控制台", "未从 openclaw.json 解析到可用地址，请检查 gateway.port 与 auth 配置。");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _consoleUrl,
                UseShellExecute = true
            });
        }

        private void SetBusy(bool isBusy, string text = "处理中...")
        {
            _isBusy = isBusy;
            BusyRing.IsActive = isBusy;
            BusyRing.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            BusyText.Text = text;
            BusyText.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

            if (isBusy)
            {
                RunButton.Visibility = Visibility.Collapsed;
                HelpButton.Visibility = Visibility.Collapsed;
                RestartButton.Visibility = Visibility.Collapsed;
                OpenConsoleButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                RunButton.Visibility = Visibility.Visible;
                HelpButton.Visibility = Visibility.Visible;
                UpdateVisualState();
            }
        }

        private void UpdateOpenConsoleButtonState()
        {
            var isRunning = OpenClawWatcher.IsRunning;
            OpenConsoleButton.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
            OpenConsoleButton.IsEnabled = isRunning && !_isBusy && !string.IsNullOrWhiteSpace(_consoleUrl);
        }

        private void UpdateVisualState()
        {
            if (!_isInitialized)
            {
                StatusText.Text = "首次启动：请先初始化";
                RunButtonText.Text = "初始化";
                RunButtonIcon.Glyph = "\uE8FB";
                RunButton.Background = new SolidColorBrush(Colors.DodgerBlue);
                RunButton.Foreground = new SolidColorBrush(Colors.White);
                RestartButton.Visibility = Visibility.Collapsed;
                UpdateOpenConsoleButtonState();
                return;
            }

            if (_isRunning)
            {
                RestartButton.Visibility = Visibility.Visible;
                UpdateOpenConsoleButtonState();
                StatusText.Text = "OpenClaw 运行中";
                RunButtonText.Text = "停止";
                RunButtonIcon.Glyph = "\uE71A";
                RunButton.Background = new SolidColorBrush(Colors.IndianRed);
                RunButton.Foreground = new SolidColorBrush(Colors.White);
                return;
            }

            RestartButton.Visibility = Visibility.Collapsed;
            UpdateOpenConsoleButtonState();

            StatusText.Text = "OpenClaw 已就绪";
            RunButtonText.Text = "运行";
            RunButtonIcon.Glyph = "\uE768";
            RunButton.Background = new SolidColorBrush(Colors.SeaGreen);
            RunButton.Foreground = new SolidColorBrush(Colors.White);
        }

        private async Task ShowDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };

            _ = dialog.ShowAsync();
            await Task.CompletedTask;
        }

    }
}
