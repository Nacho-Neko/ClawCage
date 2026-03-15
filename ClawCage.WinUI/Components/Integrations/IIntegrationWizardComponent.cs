using System.Collections.Generic;

namespace ClawCage.WinUI.Components.Integrations
{
    internal interface IIntegrationWizardComponent
    {
        /// <summary>Plugin ID used for install/uninstall and plugin status matching.</summary>
        string Key { get; }

        /// <summary>
        /// Channel key used in <c>openclaw.json</c> <c>channels</c> array.
        /// Defaults to <see cref="Key"/> when the plugin ID and config key are the same.
        /// </summary>
        string ConfigKey => Key;

        string Title { get; }
        string Description { get; }
        string Glyph { get; }
        string? IconResourceName { get; }
        string NpmPackageName { get; }

        /// <summary>
        /// Defines the configuration fields for this integration.
        /// Used to generate default channel config and configuration forms.
        /// </summary>
        IReadOnlyList<ChannelConfigField> ConfigFields { get; }
    }
}
