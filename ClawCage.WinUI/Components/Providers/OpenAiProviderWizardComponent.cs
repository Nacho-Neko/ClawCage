using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class OpenAiProviderWizardComponent : IProviderWizardComponent
    {
        public string Key => "openai";
        public string Title => "OpenAI";
        public string Description => "OpenAI 官方";
        public bool IsCustom => false;
        public string? DefaultBaseUrl => null;
        public string? DefaultApi => "openai-completions";

        public Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft)
            => ProviderDialogHelper.ShowBasicProviderDialogAsync(xamlRoot, draft);
    }
}
