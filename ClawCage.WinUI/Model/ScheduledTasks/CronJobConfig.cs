using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model.ScheduledTasks
{
    public class CronJobConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = "main";

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("deleteAfterRun")]
        public bool DeleteAfterRun { get; set; }

        [JsonPropertyName("clearAgentOverride")]
        public bool ClearAgentOverride { get; set; }

        [JsonPropertyName("sessionKey")]
        public string? SessionKey { get; set; }

        [JsonPropertyName("createdAtMs")]
        public long CreatedAtMs { get; set; }

        [JsonPropertyName("updatedAtMs")]
        public long UpdatedAtMs { get; set; }

        [JsonPropertyName("schedule")]
        public CronJobSchedule? Schedule { get; set; }

        [JsonPropertyName("sessionTarget")]
        public string SessionTarget { get; set; } = "main";

        [JsonPropertyName("wakeMode")]
        public string WakeMode { get; set; } = "next-heartbeat";

        [JsonPropertyName("payload")]
        public CronJobPayload? Payload { get; set; }

        [JsonPropertyName("delivery")]
        public CronJobDelivery? Delivery { get; set; }

        [JsonPropertyName("failureAlert")]
        public CronJobFailureAlert? FailureAlert { get; set; }

        [JsonPropertyName("state")]
        public CronJobState? State { get; set; }
    }

    public class CronJobSchedule
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "every";

        [JsonPropertyName("everyMs")]
        public long EveryMs { get; set; }

        [JsonPropertyName("anchorMs")]
        public long AnchorMs { get; set; }
    }

    public class CronJobPayload
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "systemEvent";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }

        [JsonPropertyName("lightContext")]
        public bool LightContext { get; set; }
    }

    public class CronJobDelivery
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "none";

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; }

        [JsonPropertyName("bestEffort")]
        public bool BestEffort { get; set; }
    }

    public class CronJobFailureAlert
    {
        [JsonPropertyName("after")]
        public int After { get; set; } = 2;

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("cooldownMs")]
        public long CooldownMs { get; set; } = 3600000;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "announce";
    }

    public class CronJobState
    {
        [JsonPropertyName("nextRunAtMs")]
        public long NextRunAtMs { get; set; }

        [JsonPropertyName("lastRunAtMs")]
        public long LastRunAtMs { get; set; }

        [JsonPropertyName("lastRunStatus")]
        public string LastRunStatus { get; set; } = "ok";

        [JsonPropertyName("lastStatus")]
        public string LastStatus { get; set; } = "ok";

        [JsonPropertyName("lastDurationMs")]
        public long LastDurationMs { get; set; }

        [JsonPropertyName("lastDeliveryStatus")]
        public string LastDeliveryStatus { get; set; } = "not-requested";

        [JsonPropertyName("consecutiveErrors")]
        public int ConsecutiveErrors { get; set; }
    }
}
