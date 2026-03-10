using ClawCage.WinUI.Model;
using Microsoft.UI.Xaml;
using System.Linq;
using System.Threading.Tasks;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Components
{
    internal static class AddModelProviderComponent
    {
        internal sealed class ApplyResult
        {
            public bool Applied { get; set; }
            public string? ErrorMessage { get; set; }
            public string? SuccessMessage { get; set; }
        }

        internal static async Task<ApplyResult> ShowAndApplyAsync(XamlRoot xamlRoot, Models modelsConfig)
        {
            modelsConfig.Providers ??= [];
            var dialogResult = await AddModelWizardDialog.ShowAsync(
                xamlRoot,
                modelsConfig.Providers.Keys.ToList());

            if (dialogResult is null || !dialogResult.Succeeded)
                return new ApplyResult();

            if (dialogResult.IsNewProvider)
            {
                if (string.IsNullOrWhiteSpace(dialogResult.ProviderKey))
                    return new ApplyResult { ErrorMessage = "Provider Key 不能为空。" };

                if (modelsConfig.Providers.ContainsKey(dialogResult.ProviderKey))
                    return new ApplyResult { ErrorMessage = "该 Provider Key 已存在，请更换。" };

                modelsConfig.Providers[dialogResult.ProviderKey] = new Provider
                {
                    ApiKey = dialogResult.ApiKey,
                    BaseUrl = dialogResult.BaseUrl,
                    Api = dialogResult.Api,
                    Models = dialogResult.PresetModels?.ToList() ?? []
                };
            }

            if (!modelsConfig.Providers.TryGetValue(dialogResult.ProviderKey, out var targetProvider) || targetProvider is null)
                return new ApplyResult { ErrorMessage = "未找到目标 Provider。" };

            if (dialogResult.PresetModels is { Count: > 0 })
            {
                targetProvider.Models = dialogResult.PresetModels.ToList();
                return new ApplyResult
                {
                    Applied = true,
                    SuccessMessage = "已完成添加供应商。"
                };
            }

            targetProvider.Models ??= [];
            targetProvider.Models.Add(new OpenClawModel
            {
                Id = dialogResult.ModelId,
                Name = dialogResult.ModelId,
                Input = ["text"],
                Reasoning = false,
                ContextWindow = 0,
                MaxTokens = 0,
                Cost = new Cost()
            });

            return new ApplyResult
            {
                Applied = true,
                SuccessMessage = "已完成添加模型。"
            };
        }
    }
}
