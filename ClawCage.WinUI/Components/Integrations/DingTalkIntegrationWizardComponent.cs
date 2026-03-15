using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class DingTalkIntegrationWizardComponent : IIntegrationWizardComponent
    {
        public string Key => "dingtalk";
        public string Title => "钉钉";
        public string Description => "通过钉钉自定义机器人发送消息与通知";
        public string Glyph => "\uE8BD";
        public string? IconResourceName => "dingtalk";

        public async Task<bool> ConfigureNewIntegrationAsync(XamlRoot xamlRoot, IntegrationDraft draft)
        {
            draft.Type = "DingTalk";
            draft.Name = "钉钉";

            var nameBox = new TextBox { Header = "接入名称", PlaceholderText = "例如：研发群机器人", HorizontalAlignment = HorizontalAlignment.Stretch };
            var tokenBox = new TextBox { Header = "Webhook 地址", PlaceholderText = "https://oapi.dingtalk.com/robot/send?access_token=...", HorizontalAlignment = HorizontalAlignment.Stretch };
            var secretBox = new PasswordBox { Header = "签名密钥（可选）", PasswordRevealMode = PasswordRevealMode.Peek };

            var dialog = new ContentDialog
            {
                Title = "配置钉钉接入",
                PrimaryButtonText = "完成",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { nameBox, tokenBox, secretBox }
                }
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(tokenBox.Text))
                    args.Cancel = true;
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return false;

            draft.Name = nameBox.Text.Trim();
            draft.Config = new Dictionary<string, object>
            {
                { "webhookUrl", tokenBox.Text.Trim() },
                { "secret", secretBox.Password }
            };

            return true;
        }

        private static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
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
