using ClawCage.WinUI.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Velopack;
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
                // 显示检查更新UI
                UpdateInfoBar.Severity = InfoBarSeverity.Informational;
                UpdateInfoBar.Title = "正在检查更新";
                UpdateInfoBar.Message = "正在连接服务器检查新版本...";
                if (UpdateInfoBar.Content is not ProgressRing)
                {
                    UpdateInfoBar.Content = new ProgressRing { Width = 20, Height = 20, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                }
                UpdateInfoBar.IsOpen = true;

                var mgr = new UpdateManager("https://claw.mekou.net/releases");
                var newVersionInfo = await mgr.CheckForUpdatesAsync();

                if (newVersionInfo == null)
                {
                    UpdateInfoBar.Severity = InfoBarSeverity.Success;
                    UpdateInfoBar.Title = "检查完成";
                    UpdateInfoBar.Message = "当前已是最新版本";
                    UpdateInfoBar.Content = null;
                    await Task.Delay(2000);
                    UpdateInfoBar.IsOpen = false;
                    return;
                }
                // 检查完毕且有新版本，隐藏UI并显示对话框
                UpdateInfoBar.IsOpen = false;
                var upcomingVersion = newVersionInfo.TargetFullRelease.Version;
                var currentVersion = mgr.CurrentVersion;
                bool forceUpdate = upcomingVersion.Major > currentVersion.Major ||
                                   (upcomingVersion.Major == currentVersion.Major && upcomingVersion.Minor > currentVersion.Minor);

                if (forceUpdate)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "发现重要更新",
                        Content = $"检测到重大版本更新 v{upcomingVersion}，为了提供更好的体验，必须进行更新。\n当前版本：v{currentVersion}",
                        PrimaryButtonText = "立即更新",
                        XamlRoot = this.Content.XamlRoot
                    };

                    await dialog.ShowAsync();
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
                        await mgr.DownloadUpdatesAsync(newVersionInfo);
                        mgr.ApplyUpdatesAndRestart(newVersionInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update checking failed: {ex}");
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
