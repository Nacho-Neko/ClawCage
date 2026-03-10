using ClawCage.WinUI.Model;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.OpenClaw
{
    internal static class OpenClawConfigService
    {
        private const string ConfigDirName = ".openclaw";
        private const string ConfigFileName = "openclaw.json";

        private static readonly object SyncRoot = new();
        private static FileSystemWatcher? _watcher;
        private static Timer? _debounceTimer;

        internal static event EventHandler? ConfigChanged;

        static OpenClawConfigService()
        {
            EnsureWatcher();
        }

        internal static string GetConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ConfigDirName,
                ConfigFileName);
        }

        internal static bool IsInitialized() => File.Exists(GetConfigPath());

        internal static async Task<JsonObject?> LoadRootAsync()
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonNode.Parse(json) as JsonObject;
        }

        internal static async Task<bool> SaveRootAsync(JsonObject root)
        {
            var path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
            RaiseConfigChanged();
            return true;
        }

        internal static async Task<string?> TryGetConsoleUrlAsync()
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

        internal static async Task<(JsonObject Root, Models Models)?> LoadModelsConfigAsync()
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

        internal static Task<bool> SaveModelsConfigAsync(JsonObject root, Models models)
        {
            models.Mode = string.IsNullOrWhiteSpace(models.Mode) ? "merge" : models.Mode;
            models.Providers ??= [];
            root["models"] = JsonSerializer.SerializeToNode(models);
            return SaveRootAsync(root);
        }

        private static bool TryGetGatewayPort(JsonObject root, out int port)
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

        private static void EnsureWatcher()
        {
            lock (SyncRoot)
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

        private static void OnWatcherEvent(object sender, FileSystemEventArgs e)
        {
            lock (SyncRoot)
            {
                _debounceTimer ??= new Timer(_ => RaiseConfigChanged());
                _debounceTimer.Change(250, Timeout.Infinite);
            }
        }

        private static void RaiseConfigChanged()
        {
            ConfigChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
