using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model
{
    public class Integrations
    {
        [JsonPropertyName("providers")]
        public Dictionary<string, Integration> Providers { get; set; }
    }

    public class Integration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("config")]
        public Dictionary<string, object> Config { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}
