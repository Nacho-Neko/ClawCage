using ClawCage.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClawCage.WinUI.Services.Tools.Helper;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class DeployPage : Page
    {
        private CancellationTokenSource? _deployCts;
        private bool _isDeploying;
        private readonly StringBuilder _logBuffer = new();
        private string? _lastFailureLogPath;

        public DeployPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await TryShowSuccessIfAlreadyDeployedAsync();
        }

        private async Task TryShowSuccessIfAlreadyDeployedAsync()
        {
            var databasePath = AppRuntimeState.DatabasePath;
            var verifyResult = await NodeJsHelper.VerifyOpenClawAsync(databasePath);
            if (!verifyResult.Success)
                return;

            AppSettings.SetBool(AppSettingKeys.IsInitialized, true);
            ShowSuccessState();
        }

        private async void CenterDeployButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDeployAsync();
        }

        private async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeploying)
            {
                _deployCts?.Cancel();
                return;
            }

            await StartDeployAsync();
        }

        private async Task StartDeployAsync()
        {
            var databasePath = AppRuntimeState.DatabasePath;

            _isDeploying = true;
            TopBar.Visibility = Visibility.Visible;
            IdlePanel.Visibility = Visibility.Collapsed;
            ConsoleHost.Visibility = Visibility.Visible;
            FailurePanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Collapsed;
            ActionButton.Content = "停止";
            ActionButton.IsEnabled = true;
            DeployProgressBar.IsIndeterminate = true;
            _logBuffer.Clear();
            _lastFailureLogPath = null;
            FailureLogLink.Content = "查看日志";
            AppendLog("[INFO] 开始部署 OpenClaw...");

            _deployCts = new CancellationTokenSource();

            NodeJsHelper.CommandResult result;
            try
            {
                result = await NodeJsHelper.NpmInstallAsync(
                    "openclaw",
                    globalInstall: true,
                    databasePath,
                    _deployCts.Token,
                    chunk => DispatcherQueue.TryEnqueue(() => AppendLog(chunk)));
            }
            finally
            {
                _deployCts?.Dispose();
                _deployCts = null;
                _isDeploying = false;
                DeployProgressBar.IsIndeterminate = false;
                ActionButton.Content = "部署";
            }

            AppendLog(result.Success ? "[INFO] 部署完成。" : "[ERROR] 部署失败。");

            if (result.Success)
            {
                AppSettings.SetBool(AppSettingKeys.IsInitialized, true);
                ShowSuccessState();
                return;
            }

            var logZipPath = await SaveFailureLogArchiveAsync(databasePath, result.ExitCode);
            ShowFailureState(logZipPath);
        }

        private void AppendLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _logBuffer.AppendLine(text);

            if (string.IsNullOrWhiteSpace(ConsoleText.Text))
                ConsoleText.Text = text;
            else
                ConsoleText.Text += $"\n{text}";

            if (ConsoleText.Parent is ScrollViewer viewer)
                viewer.ChangeView(null, double.MaxValue, null, true);
        }

        private async Task<string?> SaveFailureLogArchiveAsync(string databasePath, int exitCode)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return null;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipPath = Path.Combine(databasePath, $"deploy-failure-{timestamp}.zip");
                var entryName = $"deploy-failure-{timestamp}.log";

                var logContent = new StringBuilder()
                    .AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                    .AppendLine($"ExitCode: {exitCode}")
                    .AppendLine("--- Console Log ---")
                    .AppendLine(_logBuffer.ToString())
                    .ToString();

                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(logContent);

                AppendLog($"[INFO] 失败日志已保存: {zipPath}");
                return zipPath;
            }
            catch (Exception ex)
            {
                AppendLog($"[WARN] 保存失败日志压缩包失败: {ex.Message}");
                return null;
            }
        }

        private void ShowFailureState(string? logZipPath)
        {
            TopBar.Visibility = Visibility.Collapsed;
            ConsoleHost.Visibility = Visibility.Collapsed;
            IdlePanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Collapsed;
            FailurePanel.Visibility = Visibility.Visible;

            _lastFailureLogPath = logZipPath;
            FailureLogLink.Content = !string.IsNullOrWhiteSpace(logZipPath)
                ? Path.GetFileName(logZipPath)
                : "未生成日志";
            FailureLogLink.IsEnabled = !string.IsNullOrWhiteSpace(logZipPath);
        }

        private void ShowSuccessState()
        {
            TopBar.Visibility = Visibility.Collapsed;
            ConsoleHost.Visibility = Visibility.Collapsed;
            IdlePanel.Visibility = Visibility.Collapsed;
            FailurePanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
        }

        private async void RedeployButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDeployAsync();
        }

        private void FailureLogLink_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastFailureLogPath) || !File.Exists(_lastFailureLogPath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_lastFailureLogPath}\"",
                UseShellExecute = true
            });
        }

        private void EnterSoftwareButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(HomePage));
        }
    }
}
