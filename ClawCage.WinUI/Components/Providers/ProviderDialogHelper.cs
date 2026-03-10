using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Providers
{
    internal static class ProviderDialogHelper
    {
        internal static async Task<bool> ShowBasicProviderDialogAsync(XamlRoot xamlRoot, ProviderDraft draft)
        {
            var keyBox = new TextBox { Header = "Provider Key", Text = draft.ProviderKey };
            var apiKeyBox = new PasswordBox { Header = "API Key", Password = draft.ApiKey, PasswordRevealMode = PasswordRevealMode.Peek };

            var dialog = new ContentDialog
            {
                Title = "供应商信息",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    MinWidth = 580,
                    Spacing = 10,
                    Children =
                    {
                        keyBox,
                        apiKeyBox
                    }
                }
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return false;

            draft.ProviderKey = keyBox.Text.Trim();
            draft.ApiKey = apiKeyBox.Password;
            return true;
        }

        internal static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
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
