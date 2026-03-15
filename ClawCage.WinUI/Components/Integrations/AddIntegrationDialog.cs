using ClawCage.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class AddIntegrationDialog
    {
        internal sealed class IntegrationAddResult
        {
            public string IntegrationKey { get; set; } = string.Empty;
        }

        internal sealed class IntegrationTemplate
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Glyph { get; set; } = "\uE8F9";
            public IIntegrationWizardComponent? Component { get; set; }
            public BitmapImage? Icon { get; set; }
        }

        internal static async Task<IntegrationAddResult?> ShowAddIntegrationAsync(XamlRoot xamlRoot)
        {
            IntegrationComponentRegistry.EnsureInitialized();

            var templates = IntegrationComponentRegistry.GetAll()
                .Select(c => new IntegrationTemplate
                {
                    Key = c.Key,
                    Title = c.Title,
                    Description = c.Description,
                    Glyph = c.Glyph,
                    Component = c,
                    Icon = !string.IsNullOrEmpty(c.IconResourceName)
                        ? new BitmapImage(new Uri($"ms-appx:///Asset/Integration/{c.IconResourceName}.png"))
                        : null
                })
                .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (templates.Count == 0)
            {
                await ShowErrorAsync(xamlRoot, "没有可用的集成服务商。");
                return null;
            }

            var chooseStep = new AddIntegrationWizardChooseStep();
            chooseStep.SetIntegrations(templates);

            var chooseDialog = new ContentDialog
            {
                Title = "选择集成服务",
                PrimaryButtonText = "完成",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = chooseStep
            };

            chooseDialog.PrimaryButtonClick += (_, args) =>
            {
                if (chooseStep.GetSelected() is null)
                    args.Cancel = true;
            };

            var chooseResult = await ShowDialogAsync(chooseDialog);
            if (chooseResult != ContentDialogResult.Primary)
                return null;

            var selected = chooseStep.GetSelected();
            if (selected is null)
                return null;

            AppSettings.AddToStringList(AppSettingKeys.WaterfallPlugins, selected.Key);

            return new IntegrationAddResult
            {
                IntegrationKey = selected.Key
            };
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

        private static async Task ShowErrorAsync(XamlRoot xamlRoot, string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "关闭",
                XamlRoot = xamlRoot
            };
            await ShowDialogAsync(dialog);
        }
    }
}

