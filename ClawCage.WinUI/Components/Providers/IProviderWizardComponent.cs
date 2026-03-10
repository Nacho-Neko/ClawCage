using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class ProviderDraft
    {
        public string ProviderKey { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string Api { get; set; } = string.Empty;
        public bool SkipModelStep { get; set; }
        public List<OpenClawModel>? PresetModels { get; set; }
    }

    internal interface IProviderWizardComponent
    {
        string Key { get; }
        string Title { get; }
        string Description { get; }
        bool IsCustom { get; }
        string? DefaultBaseUrl { get; }
        string? DefaultApi { get; }

        Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft);
    }
}
