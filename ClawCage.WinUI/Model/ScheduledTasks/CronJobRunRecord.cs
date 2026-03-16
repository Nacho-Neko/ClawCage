using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model.ScheduledTasks
{
    public class CronJobRunRecord
    {
        [JsonPropertyName("ts")]
        public long Ts { get; set; }

        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("deliveryStatus")]
        public string DeliveryStatus { get; set; } = string.Empty;

        [JsonPropertyName("runAtMs")]
        public long RunAtMs { get; set; }

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; set; }

        [JsonPropertyName("nextRunAtMs")]
        public long NextRunAtMs { get; set; }
    }
}
