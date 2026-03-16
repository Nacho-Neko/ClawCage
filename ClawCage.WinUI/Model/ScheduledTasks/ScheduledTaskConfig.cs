using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model
{
    public sealed class ScheduledTaskConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("agent")]
        public string Agent { get; set; } = "main";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("every")]
        public int Every { get; set; } = 30;

        [JsonPropertyName("unit")]
        public string Unit { get; set; } = "minutes";

        [JsonPropertyName("session")]
        public string Session { get; set; } = "isolated";

        [JsonPropertyName("wake")]
        public string Wake { get; set; } = "immediate";

        [JsonPropertyName("action")]
        public string Action { get; set; } = "run_agent";

        [JsonPropertyName("timeout")]
        public int? Timeout { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("delivery")]
        public string Delivery { get; set; } = "publish";

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = "last";

        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }

        [JsonPropertyName("delete_after_run")]
        public bool DeleteAfterRun { get; set; }

        [JsonPropertyName("clear_agent_override")]
        public bool ClearAgentOverride { get; set; }

        [JsonPropertyName("session_key")]
        public string? SessionKey { get; set; }

        [JsonPropertyName("account_id")]
        public string? AccountId { get; set; }

        [JsonPropertyName("light_context")]
        public bool LightContext { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }

        [JsonPropertyName("failure_alerts")]
        public string FailureAlerts { get; set; } = "inherit";

        [JsonPropertyName("best_effort")]
        public bool BestEffort { get; set; }
    }
}
