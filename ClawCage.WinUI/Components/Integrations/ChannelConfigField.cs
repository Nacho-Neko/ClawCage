namespace ClawCage.WinUI.Components.Integrations
{
    /// <summary>
    /// Describes a single configuration field for a channel integration.
    /// Used to auto-generate default config and configuration forms.
    /// </summary>
    internal sealed class ChannelConfigField
    {
        /// <summary>JSON property name, e.g. "clientId".</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Display label shown in the form, e.g. "Client ID".</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Hint / placeholder text shown below or inside the field.</summary>
        public string Hint { get; init; } = string.Empty;

        /// <summary>Default value (string, bool, int, or double).</summary>
        public object? DefaultValue { get; init; }

        /// <summary>Field type used to choose the right UI control.</summary>
        public ChannelConfigFieldType FieldType { get; init; } = ChannelConfigFieldType.String;

        /// <summary>Whether this field is required.</summary>
        public bool Required { get; init; }

        /// <summary>Available options for <see cref="ChannelConfigFieldType.Combo"/> fields.</summary>
        public string[]? ComboOptions { get; init; }

        /// <summary>
        /// When set, this field is only visible if the field named <see cref="VisibleWhen"/>
        /// has a value equal to <see cref="VisibleWhenValue"/>.
        /// </summary>
        public string? VisibleWhen { get; init; }

        /// <summary>The value that <see cref="VisibleWhen"/> must match for this field to be visible.</summary>
        public string? VisibleWhenValue { get; init; }
    }

    internal enum ChannelConfigFieldType
    {
        String,
        Bool,
        Int,
        Combo,
        StringArray
    }
}
