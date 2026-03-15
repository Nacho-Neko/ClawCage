using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class LarkIntegrationWizardComponent : IIntegrationWizardComponent
    {
        public string Key => "lark";
        public string Title => "飞书";
        public string Description => "通过飞书自定义机器人发送消息与通知";
        public string Glyph => "\uE8C1";
        public string? IconResourceName => "lark";

        public async Task<bool> ConfigureNewIntegrationAsync(XamlRoot xamlRoot, IntegrationDraft draft)
        {
            draft.Type = "Lark";
            draft.Name = "飞书";

            var nameBox = new TextBox { Header = "接入名称", PlaceholderText = "例如：项目群机器人", HorizontalAlignment = HorizontalAlignment.Stretch };
            var webhookBox = new TextBox { Header = "Webhook 地址", PlaceholderText = "https://open.feishu.cn/open-apis/bot/v2/hook/...", HorizontalAlignment = HorizontalAlignment.Stretch };

            var dialog = new ContentDialog
            {
                Title = "配置飞书接入",
                PrimaryButtonText = "完成",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { nameBox, webhookBox }
                }
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(webhookBox.Text))
                    args.Cancel = true;
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return false;

            draft.Name = nameBox.Text.Trim();
            draft.Config = new Dictionary<string, object>
            {
                { "webhookUrl", webhookBox.Text.Trim() }
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
