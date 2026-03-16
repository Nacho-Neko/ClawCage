using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Agents
{
    internal static class AgentDialogs
    {
        internal static Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
        {
            var tcs = new TaskCompletionSource<ContentDialogResult>();
            var op = dialog.ShowAsync();
            op.Completed = (o, s) =>
            {
                tcs.TrySetResult(s == AsyncStatus.Completed ? o.GetResults() : ContentDialogResult.None);
            };
            return tcs.Task;
        }

        internal static Task<ContentDialogResult> ShowInfoAsync(XamlRoot xamlRoot, string title, string content)
        {
            return ShowAsync(new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = xamlRoot
            });
        }

        internal static Task<ContentDialogResult> ShowConfirmDeleteAsync(XamlRoot xamlRoot, string content)
        {
            return ShowAsync(new ContentDialog
            {
                Title = "确认删除",
                Content = content,
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            });
        }
    }
}
