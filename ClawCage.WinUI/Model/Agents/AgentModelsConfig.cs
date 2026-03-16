using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model.Agents
{
    public class AgentModelsConfig
    {
        [JsonPropertyName("providers")]
        public Dictionary<string, AgentProvider> Providers { get; set; } = new();
    }

    public class AgentProvider
    {
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("api")]
        public string Api { get; set; } = string.Empty;

        [JsonPropertyName("models")]
        public List<AgentModel> Models { get; set; } = new();
    }

    public class AgentModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("reasoning")]
        public bool Reasoning { get; set; }

        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = new();

        [JsonPropertyName("cost")]
        public AgentModelCost Cost { get; set; } = new();

        [JsonPropertyName("contextWindow")]
        public int ContextWindow { get; set; }

        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("api")]
        public string? Api { get; set; }
    }

    public class AgentModelCost
    {
        [JsonPropertyName("input")]
        public double Input { get; set; }

        [JsonPropertyName("output")]
        public double Output { get; set; }

        [JsonPropertyName("cacheRead")]
        public double CacheRead { get; set; }

        [JsonPropertyName("cacheWrite")]
        public double CacheWrite { get; set; }
    }
}
