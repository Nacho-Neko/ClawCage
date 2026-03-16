using ClawCage.WinUI.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.OpenClaw
{
    internal sealed class OpenClawConfigService
    {
        private const string ConfigDirName = ".openclaw";
        private const string ConfigFileName = "openclaw.json";

        private readonly object _syncRoot = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private volatile bool _isSelfWriting;

        internal event EventHandler? ConfigChanged;

        internal void Initialize()
        {
            EnsureWatcher();
        }

        internal string GetConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ConfigDirName,
                ConfigFileName);
        }

        internal bool IsInitialized() => File.Exists(GetConfigPath());

        internal async Task<JsonObject?> LoadRootAsync()
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonNode.Parse(json) as JsonObject;
        }

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal async Task<bool> SaveRootAsync(JsonObject root)
        {
            var path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = root.ToJsonString(WriteOptions);

            _isSelfWriting = true;
            try
            {
                await File.WriteAllTextAsync(path, json);
            }
            finally
            {
                // Delay reset so the FileSystemWatcher event (which fires asynchronously) is still suppressed
                _ = Task.Delay(300).ContinueWith(_ => _isSelfWriting = false);
            }

            return true;
        }

        /// <summary>
        /// Re-reads the config file, sets <paramref name="key"/> to <paramref name="value"/>,
        /// then writes the merged result. This prevents overwriting unrelated sections
        /// when the caller holds a stale root.
        /// </summary>
        private async Task<bool> MergeAndSaveAsync(string key, JsonNode? value)
        {
            var root = await LoadRootAsync() ?? new JsonObject();
            root[key] = value;
            return await SaveRootAsync(root);
        }

        internal async Task<string?> TryGetConsoleUrlAsync()
        {
            var root = await LoadRootAsync();
            if (root is null)
                return null;

            if (!TryGetGatewayPort(root, out var port))
                return null;

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"http://localhost");
            stringBuilder.Append($":{port}/");
            if (TryGetToken(root, out var token))
                stringBuilder.Append($"#token={Uri.EscapeDataString(token)}");
            return stringBuilder.ToString();
        }

        internal async Task<(JsonObject Root, Models Models)?> LoadModelsConfigAsync()
        {
            var root = await LoadRootAsync();
            if (root is null)
                return null;

            var modelsNode = root["models"];
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var models = modelsNode?.Deserialize<Models>(options) ?? new Models();
            models.Mode ??= "merge";
            models.Providers ??= [];

            return (root, models);
        }

        internal Task<bool> SaveModelsConfigAsync(JsonObject root, Models models)
        {
            models.Mode = string.IsNullOrWhiteSpace(models.Mode) ? "merge" : models.Mode;
            models.Providers ??= [];
            return MergeAndSaveAsync("models", JsonSerializer.SerializeToNode(models, WriteOptions));
        }

        internal async Task<(JsonObject Root, Dictionary<string, ChannelEntry> Channels)?> LoadChannelsConfigAsync()
        {
            var root = await LoadRootAsync();
            if (root is null)
                return null;

            var channels = new Dictionary<string, ChannelEntry>(StringComparer.OrdinalIgnoreCase);

            if (root["channels"] is JsonObject channelsObj)
            {
                foreach (var prop in channelsObj)
                {
                    var data = prop.Value is JsonObject dataObj
                        ? JsonNode.Parse(dataObj.ToJsonString()) as JsonObject ?? []
                        : [];
                    channels[prop.Key] = new ChannelEntry { Key = prop.Key, Data = data };
                }
            }

            return (root, channels);
        }

        internal Task<bool> SaveChannelsConfigAsync(JsonObject root, Dictionary<string, ChannelEntry> channels)
        {
            var channelsObj = new JsonObject();
            foreach (var entry in channels.Values)
            {
                channelsObj[entry.Key] = JsonNode.Parse(entry.Data.ToJsonString(WriteOptions));
            }
            return MergeAndSaveAsync("channels", channelsObj);
        }

        internal async Task<(JsonObject Root, PluginsConfig Plugins)?> LoadPluginsConfigAsync()
        {
            var root = await LoadRootAsync();
            if (root is null)
                return null;

            var pluginsNode = root["plugins"];
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var plugins = pluginsNode?.Deserialize<PluginsConfig>(options) ?? new PluginsConfig();
            plugins.Allow ??= [];

            return (root, plugins);
        }

        internal Task<bool> SavePluginsConfigAsync(JsonObject root, PluginsConfig plugins)
        {
            plugins.Allow ??= [];
            return MergeAndSaveAsync("plugins", JsonSerializer.SerializeToNode(plugins, WriteOptions));
        }

        internal async Task<(JsonObject Root, List<ScheduledTaskConfig> Tasks)?> LoadScheduledTasksAsync()
        {
            var root = await LoadRootAsync();
            if (root is null)
                return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var tasks = new List<ScheduledTaskConfig>();

            if (root["scheduled_tasks"] is JsonArray tasksArray)
            {
                foreach (var node in tasksArray)
                {
                    if (node is null) continue;
                    var task = node.Deserialize<ScheduledTaskConfig>(options);
                    if (task is not null)
                        tasks.Add(task);
                }
            }

            return (root, tasks);
        }

        internal Task<bool> SaveScheduledTasksAsync(List<ScheduledTaskConfig> tasks)
        {
            var array = JsonSerializer.SerializeToNode(tasks, WriteOptions);
            return MergeAndSaveAsync("scheduled_tasks", array);
        }

        private static bool TryGetGatewayPort(JsonObject root, out int port) // pure helper – keep static
        {
            port = 0;
            if (root["gateway"] is not JsonObject gateway)
                return false;

            if (gateway["port"] is not JsonValue portNode)
                return false;

            if (portNode.TryGetValue<int>(out var intPort) && intPort > 0)
            {
                port = intPort;
                return true;
            }

            if (portNode.TryGetValue<string>(out var strPort)
                && int.TryParse(strPort, out var parsed)
                && parsed > 0)
            {
                port = parsed;
                return true;
            }

            return false;
        }

        private static bool TryGetToken(JsonObject root, out string token)
        {
            token = string.Empty;

            if (root["gateway"] is not JsonObject gateway)
                return false;

            if (gateway["auth"] is not JsonObject auth)
                return false;

            if (!TryGetString(auth, "mode", out var mode)
                || !string.Equals(mode, "token", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetString(auth, "token", out var authToken)
                || string.IsNullOrWhiteSpace(authToken))
                return false;

            token = authToken;
            return true;
        }

        private static bool TryGetString(JsonObject node, string key, out string value)
        {
            value = string.Empty;
            if (node[key] is not JsonValue valueNode)
                return false;

            if (!valueNode.TryGetValue<string>(out var strValue)
                || string.IsNullOrWhiteSpace(strValue))
                return false;

            value = strValue;
            return true;
        }

        private void EnsureWatcher()
        {
            lock (_syncRoot)
            {
                if (_watcher is not null)
                    return;

                var configPath = GetConfigPath();
                var directory = Path.GetDirectoryName(configPath)!;
                Directory.CreateDirectory(directory);

                _watcher = new FileSystemWatcher(directory, ConfigFileName)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnWatcherEvent;
                _watcher.Created += OnWatcherEvent;
                _watcher.Deleted += OnWatcherEvent;
                _watcher.Renamed += OnWatcherEvent;
            }
        }

        private void OnWatcherEvent(object sender, FileSystemEventArgs e)
        {
            if (_isSelfWriting)
                return;

            lock (_syncRoot)
            {
                _debounceTimer ??= new Timer(_ => RaiseConfigChanged());
                _debounceTimer.Change(250, Timeout.Infinite);
            }
        }

        private void RaiseConfigChanged()
        {
            ConfigChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
