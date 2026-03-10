using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class GeminiProviderWizardComponent : IProviderWizardComponent
    {
        public string Key => "gemini";
        public string Title => "Gemini";
        public string Description => "Google Gemini";
        public bool IsCustom => false;
        public string? DefaultBaseUrl => null;
        public string? DefaultApi => "google-generative-ai";

        public Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft)
            => ProviderDialogHelper.ShowBasicProviderDialogAsync(xamlRoot, draft);
    }
}
