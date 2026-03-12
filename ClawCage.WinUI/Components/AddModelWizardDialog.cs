using ClawCage.WinUI.Components.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Components
{
    internal sealed class AddModelWizardDialog
    {
        internal sealed class ProviderTemplate
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? DefaultBaseUrl { get; set; }
            public string? DefaultApi { get; set; }
            public bool IsCustom { get; set; }
            public IProviderWizardComponent? Component { get; set; }
            public BitmapImage? Icon { get; set; }
        }

        internal sealed class ProviderAddResult
        {
            public string ProviderKey { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public string Api { get; set; } = string.Empty;
            public List<OpenClawModel>? PresetModels { get; set; }
        }

        internal sealed class ModelAddResult
        {
            public string ModelId { get; set; } = string.Empty;
            public int ContextWindow { get; set; }
            public int MaxTokens { get; set; }
        }

        internal static async Task<ProviderAddResult?> ShowAddProviderAsync(XamlRoot xamlRoot)
        {
            ProviderComponentRegistry.EnsureInitialized();

            var newProviderItems = ProviderComponentRegistry.GetAll()
                .Select(c => new ProviderTemplate
                {
                    Key = c.Key,
                    Title = c.Title,
                    Description = c.Description,
                    DefaultApi = c.DefaultApi,
                    DefaultBaseUrl = c.DefaultBaseUrl,
                    IsCustom = c.IsCustom,
                    Component = c,
                    Icon = !string.IsNullOrEmpty(c.IconResourceName)
                        ? new BitmapImage(new Uri($"ms-appx:///Asset/Providers/{c.IconResourceName}.png"))
                        : null
                })
                .OrderBy(x => x.IsCustom ? 1 : 0)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var chooseStep = new AddModelWizardChooseStep();
            chooseStep.SetNewProviders(newProviderItems);
            chooseStep.ShowOnlyNewProviders();

            var chooseDialog = new ContentDialog
            {
                Title = "选择供应商",
                PrimaryButtonText = "下一步",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = chooseStep
            };

            var chooseResult = await ShowDialogAsync(chooseDialog);
            if (chooseResult != ContentDialogResult.Primary)
                return null;

            var selectedProvider = chooseStep.GetSelectedProvider();
            if (selectedProvider is null)
                return null;

            var draft = new ProviderDraft
            {
                ProviderKey = selectedProvider.IsCustom ? string.Empty : selectedProvider.Key,
                Api = selectedProvider.DefaultApi ?? string.Empty,
                BaseUrl = selectedProvider.DefaultBaseUrl ?? string.Empty
            };

            if (selectedProvider.Component is null)
                return null;

            var configured = await selectedProvider.Component.ConfigureNewProviderAsync(xamlRoot, draft);
            if (!configured)
                return null;

            return new ProviderAddResult
            {
                ProviderKey = draft.ProviderKey,
                ApiKey = draft.ApiKey,
                BaseUrl = draft.BaseUrl,
                Api = draft.Api,
                PresetModels = draft.SkipModelStep ? draft.PresetModels : null
            };
        }

        internal static async Task<ModelAddResult?> ShowAddModelAsync(XamlRoot xamlRoot)
        {
            var modelStep = new AddModelWizardModelStep();
            var modelDialog = new ContentDialog
            {
                Title = "添加模型",
                PrimaryButtonText = "完成",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = modelStep
            };

            modelDialog.PrimaryButtonClick += (_, args) =>
            {
                if (!modelStep.Validate())
                    args.Cancel = true;
            };

            var result = await ShowDialogAsync(modelDialog);
            if (result != ContentDialogResult.Primary)
                return null;

            return new ModelAddResult
            {
                ModelId = modelStep.ModelId,
                ContextWindow = modelStep.ContextWindow,
                MaxTokens = modelStep.MaxTokens
            };
        }

        internal static async Task<bool> ShowAddModelConfirmAsync(XamlRoot xamlRoot)
        {
            var dialog = new ContentDialog
            {
                Title = "添加模型",
                Content = "供应商已创建，是否立即为其添加模型？",
                PrimaryButtonText = "添加模型",
                CloseButtonText = "暂不",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };
            return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
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
