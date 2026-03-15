using System.Text.Json.Nodes;

namespace ClawCage.WinUI.Model
{
    /// <summary>
    /// Represents a single channel entry inside the <c>channels</c> array.
    /// Each entry is a JSON object with a single key (e.g. "dingtalk") whose
    /// value is a <see cref="JsonObject"/> with integration-specific settings.
    /// </summary>
    public sealed class ChannelEntry
    {
        /// <summary>Integration key, e.g. "dingtalk", "lark".</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>The raw JSON config for this channel (shape varies per integration).</summary>
        public JsonObject Data { get; set; } = [];

        /// <summary>
        /// Convenience accessor for the common <c>enabled</c> field present in every channel.
        /// Reads from / writes to <see cref="Data"/>.
        /// </summary>
        public bool Enabled
        {
            get => Data.TryGetPropertyValue("enabled", out var node)
                && node is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            set => Data["enabled"] = value;
        }
    }
}
