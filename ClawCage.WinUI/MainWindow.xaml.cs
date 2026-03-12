using ClawCage.WinUI.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Exceptions;
using Windows.Graphics;

namespace ClawCage.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            VelopackApp.Build().Run();

            InitializeComponent();

            const int width = 1280;
            const int height = 800;
            var workArea = DisplayArea
                .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)
                .WorkArea;
            AppWindow.MoveAndResize(new RectInt32(
                (workArea.Width - width) / 2,
                (workArea.Height - height) / 2,
                width, height));

            AppRuntimeState.Initialize();

            if (AppSettings.GetBool(AppSettingKeys.IsInitialized))
            {
                RootFrame.Navigate(typeof(Pages.HomePage));
            }
            else
            {
                RootFrame.Navigate(typeof(Pages.SetupPage));
            }

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += MainWindow_Loaded;
            }
        }

        private bool _hasCheckedUpdates;

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasCheckedUpdates) return;
            _hasCheckedUpdates = true;

            await CheckUpdatesAsync();
        }

        private async Task CheckUpdatesAsync()
        {
            try
            {
                UpdateInfoBar.IsOpen = false;
                UpdateCheckingText.Text = "正在连接服务器检查新版本...";
                UpdateCheckingBanner.Visibility = Visibility.Visible;

                var mgr = new UpdateManager("https://claw.mekou.net/releases");
                var newVersionInfo = await mgr.CheckForUpdatesAsync();

                if (newVersionInfo == null)
                {
                    UpdateCheckingBanner.Visibility = Visibility.Collapsed;
                    UpdateInfoBar.Severity = InfoBarSeverity.Success;
                    UpdateInfoBar.Title = "检查完成";
                    UpdateInfoBar.Message = "当前已是最新版本";
                    UpdateInfoBar.IsOpen = true;
                    await Task.Delay(2000);
                    UpdateInfoBar.IsOpen = false;
                    return;
                }
                // 检查完毕且有新版本，隐藏UI并显示对话框
                UpdateCheckingBanner.Visibility = Visibility.Collapsed;
                UpdateInfoBar.IsOpen = false;
                var upcomingVersion = newVersionInfo.TargetFullRelease.Version;
                var currentVersion = mgr.CurrentVersion;
                bool forceUpdate = upcomingVersion.Major > currentVersion.Major ||
                                   (upcomingVersion.Major == currentVersion.Major && upcomingVersion.Minor > currentVersion.Minor);

                if (forceUpdate)
                {
                    var cts = new System.Threading.CancellationTokenSource();
                    var forceExitTask = Task.Delay(TimeSpan.FromMinutes(1), cts.Token).ContinueWith(_ =>
                    {
                        DispatcherQueue.TryEnqueue(() => Application.Current.Exit());
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                    var dialog = new ContentDialog
                    {
                        Title = "发现重要更新",
                        Content = $"检测到重大版本更新 v{upcomingVersion}，为了提供更好的体验，必须进行更新。\n当前版本：v{currentVersion}\n\n若 1 分钟内未操作，应用将自动关闭。",
                        PrimaryButtonText = "立即更新",
                        XamlRoot = this.Content.XamlRoot
                    };

                    await dialog.ShowAsync();
                    await cts.CancelAsync();

                    UpdateCheckingText.Text = "正在下载更新...";
                    UpdateCheckingBanner.Visibility = Visibility.Visible;
                    await mgr.DownloadUpdatesAsync(newVersionInfo);
                    mgr.ApplyUpdatesAndRestart(newVersionInfo);
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "发现新版本",
                        Content = $"检测到新版本 v{upcomingVersion}，是否立即更新？\n当前版本：v{currentVersion}",
                        PrimaryButtonText = "立即更新",
                        CloseButtonText = "稍后",
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        UpdateCheckingText.Text = "正在下载更新...";
                        UpdateCheckingBanner.Visibility = Visibility.Visible;
                        await mgr.DownloadUpdatesAsync(newVersionInfo);
                        mgr.ApplyUpdatesAndRestart(newVersionInfo);
                    }
                }
            }
            catch (NotInstalledException)
            {
                UpdateCheckingBanner.Visibility = Visibility.Collapsed;
                return;
            }
            catch (Exception ex)
            {
                UpdateCheckingBanner.Visibility = Visibility.Collapsed;
                UpdateInfoBar.IsOpen = false;

                var dialog = new ContentDialog
                {
                    Title = "检查更新失败",
                    Content = "无法连接到更新服务器或检查失败。为了保证使用体验和版本一致性，必须成功检查更新后才能继续使用。\n\n错误信息：" + ex.Message,
                    PrimaryButtonText = "重试",
                    CloseButtonText = "退出",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _ = CheckUpdatesAsync();
                }
                else
                {
                    Application.Current.Exit();
                }
            }
        }
    }
}
