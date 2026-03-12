using ClawCage.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components
{
    public sealed partial class OpenClawInitModeDialog : UserControl
    {
        private const string RunModeGateway = "gateway";
        private const string RunModeNode = "node";

        public OpenClawInitModeDialog()
        {
            InitializeComponent();

            var runMode = AppSettings.GetString(AppSettingKeys.RunMode);
            GatewayRadio.IsChecked = runMode != RunModeNode;
            NodeRadio.IsChecked = runMode == RunModeNode;
            HostBox.Text = AppSettings.GetString(AppSettingKeys.NodeHost) ?? string.Empty;
            PortBox.Text = AppSettings.GetString(AppSettingKeys.NodePort) ?? string.Empty;
            UpdateNodeSettingsVisibility();
        }

        internal static async Task<bool> ShowAndSaveAsync(XamlRoot xamlRoot)
        {
            var content = new OpenClawInitModeDialog();
            var dialog = new ContentDialog
            {
                Title = "初始化模式",
                PrimaryButtonText = "确认初始化",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = content
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (!content.Validate())
                    args.Cancel = true;
            };

            var result = await ShowDialogWithResultAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return false;

            content.Save();

            return true;
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateNodeSettingsVisibility();
            ClearHint();
        }

        private void ShowHint(string message)
        {
            HintBar.Message = message;
            HintBar.IsOpen = true;
        }

        private void ClearHint()
        {
            HintBar.Message = string.Empty;
            HintBar.IsOpen = false;
        }

        private void UpdateNodeSettingsVisibility()
        {
            var isNodeMode = NodeRadio.IsChecked == true;

            NodeSettingsPanel.Visibility = isNodeMode ? Visibility.Visible : Visibility.Collapsed;

            GatewayCardBorder.BorderThickness = isNodeMode ? new Thickness(1) : new Thickness(2);
            NodeCardBorder.BorderThickness = isNodeMode ? new Thickness(2) : new Thickness(1);
            GatewayCardBorder.Opacity = isNodeMode ? 0.75 : 1;
            NodeCardBorder.Opacity = isNodeMode ? 1 : 0.75;
        }

        private bool Validate()
        {
            var selectedMode = NodeRadio.IsChecked == true ? RunModeNode : RunModeGateway;
            if (selectedMode != RunModeNode)
                return true;

            var host = HostBox.Text.Trim();
            var portText = PortBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                ShowHint("Host 不能为空。");
                return false;
            }

            if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
            {
                ShowHint("端口必须是 1-65535 之间的数字。");
                return false;
            }

            return true;
        }

        private void Save()
        {
            var mode = NodeRadio.IsChecked == true ? RunModeNode : RunModeGateway;
            AppSettings.SetString(AppSettingKeys.RunMode, mode);
            if (mode == RunModeNode)
            {
                AppSettings.SetString(AppSettingKeys.NodeHost, HostBox.Text.Trim());
                AppSettings.SetString(AppSettingKeys.NodePort, PortBox.Text.Trim());
            }
            else
            {
                AppSettings.SetString(AppSettingKeys.NodeHost, null);
                AppSettings.SetString(AppSettingKeys.NodePort, null);
            }
        }

        private static Task<ContentDialogResult> ShowDialogWithResultAsync(ContentDialog dialog)
        {
            var tcs = new TaskCompletionSource<ContentDialogResult>();
            var operation = dialog.ShowAsync();
            operation.Completed = (op, status) =>
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult(op.GetResults());
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetResult(ContentDialogResult.None);
                        break;
                    default:
                        tcs.TrySetException(new InvalidOperationException("对话框执行失败。"));
                        break;
                }
            };

            return tcs.Task;
        }
    }
}
