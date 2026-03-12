using ClawCage.WinUI.Components;
using ClawCage.WinUI.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.Linq;
using System.Threading.Tasks;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed partial class AddModelProviderViewModel : ObservableObject
    {
        private readonly XamlRoot _xamlRoot;
        private readonly Models _modelsConfig;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _succeeded;

        public AddModelProviderViewModel(XamlRoot xamlRoot, Models modelsConfig)
        {
            _xamlRoot = xamlRoot;
            _modelsConfig = modelsConfig;
        }

        [RelayCommand]
        private async Task AddProviderAsync()
        {
            Succeeded = false;
            StatusMessage = string.Empty;

            _modelsConfig.Providers ??= [];

            var providerResult = await AddModelWizardDialog.ShowAddProviderAsync(_xamlRoot);
            if (providerResult is null)
                return;

            if (string.IsNullOrWhiteSpace(providerResult.ProviderKey))
            {
                StatusMessage = "Provider Key 不能为空。";
                return;
            }

            if (_modelsConfig.Providers.ContainsKey(providerResult.ProviderKey))
            {
                StatusMessage = "该 Provider Key 已存在，请更换。";
                return;
            }

            _modelsConfig.Providers[providerResult.ProviderKey] = new Provider
            {
                ApiKey = providerResult.ApiKey,
                BaseUrl = providerResult.BaseUrl,
                Api = providerResult.Api,
                Models = providerResult.PresetModels?.ToList() ?? []
            };

            Succeeded = true;
            StatusMessage = "已完成添加供应商。";

            if (providerResult.PresetModels is { Count: > 0 })
                return;

            var confirmed = await AddModelWizardDialog.ShowAddModelConfirmAsync(_xamlRoot);
            if (!confirmed)
                return;

            await ApplyAddModelAsync(providerResult.ProviderKey);
        }

        [RelayCommand]
        private async Task AddModelToProviderAsync(string providerKey)
        {
            Succeeded = false;
            StatusMessage = string.Empty;

            await ApplyAddModelAsync(providerKey);
        }

        private async Task ApplyAddModelAsync(string providerKey)
        {
            var modelResult = await AddModelWizardDialog.ShowAddModelAsync(_xamlRoot);
            if (modelResult is null)
                return;

            _modelsConfig.Providers ??= [];

            if (!_modelsConfig.Providers.TryGetValue(providerKey, out var targetProvider) || targetProvider is null)
            {
                StatusMessage = "未找到目标 Provider。";
                return;
            }

            targetProvider.Models ??= [];
            targetProvider.Models.Add(new OpenClawModel
            {
                Id = modelResult.ModelId,
                Name = modelResult.ModelId,
                Input = ["text"],
                Reasoning = false,
                ContextWindow = modelResult.ContextWindow,
                MaxTokens = modelResult.MaxTokens,
                Cost = new Cost()
            });

            StatusMessage = "已完成添加模型。";
            Succeeded = true;
        }
    }
}
