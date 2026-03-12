using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class AnthropicProviderWizardComponent : IProviderWizardComponent
    {
        public string Key => "anthropic";
        public string Title => "Anthropic";
        public string Description => "Claude 官方";
        public string? IconResourceName => "anthropic";
        public bool IsCustom => false;
        public string? DefaultBaseUrl => null;
        public string? DefaultApi => "anthropic-messages";

        public Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft)
            => ProviderDialogHelper.ShowBasicProviderDialogAsync(xamlRoot, draft);
    }
}
