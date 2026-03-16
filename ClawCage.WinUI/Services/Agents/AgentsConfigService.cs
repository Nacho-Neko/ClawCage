using ClawCage.WinUI.Model.Agents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Agents
{
    public class AgentsConfigService
    {
        private static readonly string AgentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "agents");

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public Task<List<string>> ListAgentIdsAsync()
        {
            var ids = new List<string>();

            if (!Directory.Exists(AgentsDir))
                return Task.FromResult(ids);

            foreach (var dir in Directory.EnumerateDirectories(AgentsDir))
            {
                ids.Add(Path.GetFileName(dir));
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(ids);
        }

        public Task<bool> CreateAgentAsync(string agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Task.FromResult(false);

            var agentDir = Path.Combine(AgentsDir, agentId, "agent");
            Directory.CreateDirectory(agentDir);

            var modelsPath = Path.Combine(agentDir, "models.json");
            if (!File.Exists(modelsPath))
            {
                var empty = new AgentModelsConfig();
                var json = JsonSerializer.Serialize(empty, WriteOptions);
                File.WriteAllText(modelsPath, json, Encoding.UTF8);
            }

            return Task.FromResult(true);
        }

        public Task<bool> DeleteAgentAsync(string agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Task.FromResult(false);

            var agentDir = Path.Combine(AgentsDir, agentId);
            if (!Directory.Exists(agentDir))
                return Task.FromResult(false);

            Directory.Delete(agentDir, recursive: true);
            return Task.FromResult(true);
        }

        public async Task<AgentModelsConfig?> LoadModelsAsync(string agentId)
        {
            var path = GetModelsPath(agentId);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AgentModelsConfig>(json, ReadOptions);
        }

        public async Task<bool> SaveModelsAsync(string agentId, AgentModelsConfig config)
        {
            var path = GetModelsPath(agentId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = JsonSerializer.Serialize(config, WriteOptions);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            return true;
        }

        public async Task<bool> SaveProviderAsync(string agentId, string providerName, AgentProvider provider)
        {
            var config = await LoadModelsAsync(agentId) ?? new AgentModelsConfig();
            config.Providers[providerName] = provider;
            return await SaveModelsAsync(agentId, config);
        }

        public async Task<bool> RemoveProviderAsync(string agentId, string providerName)
        {
            var config = await LoadModelsAsync(agentId);
            if (config is null || !config.Providers.Remove(providerName))
                return false;

            return await SaveModelsAsync(agentId, config);
        }

        public async Task<bool> SaveModelAsync(string agentId, string providerName, AgentModel model)
        {
            var config = await LoadModelsAsync(agentId) ?? new AgentModelsConfig();
            if (!config.Providers.TryGetValue(providerName, out var provider))
                return false;

            var idx = provider.Models.FindIndex(m => m.Id == model.Id);
            if (idx >= 0)
                provider.Models[idx] = model;
            else
                provider.Models.Add(model);

            return await SaveModelsAsync(agentId, config);
        }

        public async Task<bool> RemoveModelAsync(string agentId, string providerName, string modelId)
        {
            var config = await LoadModelsAsync(agentId);
            if (config is null || !config.Providers.TryGetValue(providerName, out var provider))
                return false;

            var removed = provider.Models.RemoveAll(m => m.Id == modelId);
            if (removed == 0)
                return false;

            return await SaveModelsAsync(agentId, config);
        }

        private static string GetModelsPath(string agentId)
        {
            return Path.Combine(AgentsDir, agentId, "agent", "models.json");
        }
    }
}
