using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class IntegrationDraft
    {
        public string IntegrationKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, object> Config { get; set; } = new();
    }

    internal interface IIntegrationWizardComponent
    {
        string Key { get; }
        string Title { get; }
        string Description { get; }
        string Glyph { get; }
        string? IconResourceName { get; }

        Task<bool> ConfigureNewIntegrationAsync(XamlRoot xamlRoot, IntegrationDraft draft);
    }
}
