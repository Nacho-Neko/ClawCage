using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model
{
    public class AgentsConfig
    {
        [JsonPropertyName("defaults")]
        public AgentDefaults Defaults { get; set; } = new();
    }

    public class AgentDefaults
    {
        [JsonPropertyName("model")]
        public AgentDefaultModel Model { get; set; } = new();

        [JsonPropertyName("models")]
        public Dictionary<string, AgentDefaultModelEntry> Models { get; set; } = new();

        [JsonPropertyName("workspace")]
        public string Workspace { get; set; } = string.Empty;
    }

    public class AgentDefaultModel
    {
        [JsonPropertyName("primary")]
        public string Primary { get; set; } = string.Empty;
    }

    public class AgentDefaultModelEntry
    {
        [JsonPropertyName("alias")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Alias { get; set; }
    }
}
