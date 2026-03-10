using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClawCage.WinUI.Model
{
    public class Models
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("providers")]
        public Dictionary<string, Provider> Providers { get; set; }
    }

    public class Provider
    {
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; }

        [JsonPropertyName("api")]
        public string Api { get; set; }

        [JsonPropertyName("models")]
        public List<Model> Models { get; set; }
    }

    public class Model
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("reasoning")]
        public bool Reasoning { get; set; }

        [JsonPropertyName("input")]
        public List<string> Input { get; set; }

        [JsonPropertyName("cost")]
        public Cost Cost { get; set; }

        [JsonPropertyName("contextWindow")]
        public int ContextWindow { get; set; }

        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; }
    }

    public class Cost
    {
        [JsonPropertyName("input")]
        public int Input { get; set; }

        [JsonPropertyName("output")]
        public int Output { get; set; }

        [JsonPropertyName("cacheRead")]
        public int CacheRead { get; set; }

        [JsonPropertyName("cacheWrite")]
        public int CacheWrite { get; set; }
    }
}
