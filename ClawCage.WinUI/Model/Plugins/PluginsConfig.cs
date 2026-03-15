using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model
{
    public class PluginsConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("allow")]
        public List<string> Allow { get; set; }
    }
}
