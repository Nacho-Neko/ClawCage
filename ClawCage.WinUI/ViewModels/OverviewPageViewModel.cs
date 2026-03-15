using ClawCage.WinUI.Components;
using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.OpenClaw;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    public sealed partial class OverviewPageViewModel : ObservableObject
    {
        private readonly OpenClawConfigService _configService;

        private XamlRoot? _xamlRoot;
        private bool _isInitialized;
        private bool _isRunning;
        private string? _consoleUrl;
        private bool _isHelpOpening;

        [ObservableProperty] private string _statusText = "OpenClaw Overview";
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _busyText = "处理中...";

        [ObservableProperty] private string _runButtonText = "Initialize";
        [ObservableProperty] private string _runButtonGlyph = "\uE8FB";
        [ObservableProperty] private SolidColorBrush _runButtonBackground = new(Colors.DodgerBlue);
        [ObservableProperty] private SolidColorBrush _runButtonForeground = new(Colors.White);

        [ObservableProperty] private Visibility _busyVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _actionButtonsVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility _restartButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _openConsoleVisibility = Visibility.Collapsed;
        [ObservableProperty] private bool _openConsoleEnabled = false;
        [ObservableProperty] private bool _isHelpEnabled = true;

        internal OverviewPageViewModel(OpenClawConfigService configService)
        {
            _configService = configService;
        }

        internal void Initialize(XamlRoot xamlRoot) => _xamlRoot = xamlRoot;

        [RelayCommand]
        private async Task RefreshAsync()
        {
            _isInitialized = _configService.IsInitialized();
            _isRunning = _isInitialized && OpenClawWatcher.IsRunning;
            _consoleUrl = await _configService.TryGetConsoleUrlAsync();
            UpdateState();
        }

        [RelayCommand]
        private async Task RunAsync()
        {
            if (IsBusy) return;

            if (!_isInitialized)
            {
                await InitializeAsync();
                return;
            }

            if (_isRunning)
                await StopAsync();
            else
                await StartAsync();
        }

        [RelayCommand]
        private async Task RestartAsync()
        {
            if (IsBusy || !_isInitialized) return;

            SetBusy(true, "重启中...");
            var result = await OpenClawWatcher.RestartAsync(AppRuntimeState.DatabasePath);
            SetBusy(false);

            if (!result.Success)
            {
                ShowDialog("重启失败", string.IsNullOrWhiteSpace(result.Error) ? "重启失败。" : result.Error);
                return;
            }

            await RefreshAsync();
        }

        [RelayCommand]
        private void OpenConsole()
        {
            if (string.IsNullOrWhiteSpace(_consoleUrl))
            {
                ShowDialog("无法打开控制台", "未从 openclaw.json 解析到可用地址，请检查 gateway.port 与 auth 配置。");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _consoleUrl,
                UseShellExecute = true
            });
        }

        [RelayCommand]
        private void Help()
        {
            if (_isHelpOpening || !IsHelpEnabled)
                return;

            const string helpUrl = "https://space.bilibili.com/250601357";
            try
            {
                _isHelpOpening = true;
                IsHelpEnabled = false;
                Process.Start(new ProcessStartInfo
                {
                    FileName = helpUrl,
                    UseShellExecute = true
                });
            }
            finally
            {
                _ = ResetHelpButtonAsync();
            }
        }

        private async Task ResetHelpButtonAsync()
        {
            await Task.Delay(1200);
            _isHelpOpening = false;
            IsHelpEnabled = true;
        }

        private async Task InitializeAsync()
        {
            if (_xamlRoot is null) return;

            var modeConfigured = await OpenClawInitModeDialog.ShowAndSaveAsync(_xamlRoot);
            if (!modeConfigured) return;

            SetBusy(true, "初始化中...");
            await OpenClawWatcher.InitializeAsync();
            await Task.Delay(500);

            _isInitialized = _configService.IsInitialized();
            SetBusy(false);

            if (!_isInitialized)
                ShowDialog("初始化未完成", $"未检测到配置文件: {_configService.GetConfigPath()}");

            await RefreshAsync();
        }

        private async Task StartAsync()
        {
            SetBusy(true, "启动中...");
            var result = await OpenClawWatcher.StartAsync();
            SetBusy(false);

            if (!result.Success)
            {
                ShowDialog("启动失败", string.IsNullOrWhiteSpace(result.Error) ? "启动失败。" : result.Error);
                return;
            }

            await RefreshAsync();
        }

        private async Task StopAsync()
        {
            SetBusy(true, "停止中...");
            var result = await OpenClawWatcher.StopAsync();
            SetBusy(false);

            if (!result.Success)
            {
                ShowDialog("停止失败", string.IsNullOrWhiteSpace(result.Error) ? "停止失败。" : result.Error);
                return;
            }

            await RefreshAsync();
        }

        private void SetBusy(bool busy, string text = "处理中...")
        {
            IsBusy = busy;
            BusyText = text;
            UpdateButtonVisibility();
        }

        private void UpdateState()
        {
            UpdateButtonAppearance();
            UpdateButtonVisibility();
        }

        private void UpdateButtonAppearance()
        {
            if (!_isInitialized)
            {
                StatusText = "首次启动：请先初始化";
                RunButtonText = "初始化";
                RunButtonGlyph = "\uE8FB";
                RunButtonBackground = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 64, 175));
                RunButtonForeground = new SolidColorBrush(Colors.White);
            }
            else if (_isRunning)
            {
                StatusText = "OpenClaw 运行中";
                RunButtonText = "停止";
                RunButtonGlyph = "\uE71A";
                RunButtonBackground = new SolidColorBrush(Colors.IndianRed);
                RunButtonForeground = new SolidColorBrush(Colors.White);
            }
            else
            {
                StatusText = "OpenClaw 已就绪";
                RunButtonText = "运行";
                RunButtonGlyph = "\uE768";
                RunButtonBackground = new SolidColorBrush(Colors.SeaGreen);
                RunButtonForeground = new SolidColorBrush(Colors.White);
            }
        }

        private void UpdateButtonVisibility()
        {
            BusyVisibility = IsBusy ? Visibility.Visible : Visibility.Collapsed;
            ActionButtonsVisibility = IsBusy ? Visibility.Collapsed : Visibility.Visible;
            RestartButtonVisibility = (!IsBusy && _isRunning && _isInitialized) ? Visibility.Visible : Visibility.Collapsed;
            OpenConsoleVisibility = (!IsBusy && _isInitialized) ? Visibility.Visible : Visibility.Collapsed;
            OpenConsoleEnabled = _isInitialized && _isRunning && !IsBusy && !string.IsNullOrWhiteSpace(_consoleUrl);
        }

        private void ShowDialog(string title, string content)
        {
            if (_xamlRoot is null) return;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = _xamlRoot
            };

            _ = dialog.ShowAsync();
        }
    }
}
