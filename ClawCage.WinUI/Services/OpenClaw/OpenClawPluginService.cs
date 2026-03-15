using ClawCage.WinUI.Services.Tools.Helper;
using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.OpenClaw
{
    internal sealed class OpenClawPluginService
    {
        private Dictionary<string, PluginInfo> _plugins = new(StringComparer.OrdinalIgnoreCase);

        internal sealed class PluginInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
        }

        internal IReadOnlyDictionary<string, PluginInfo> Plugins => _plugins;

        internal async Task LoadAsync(CancellationToken ct = default)
        {
            _plugins = await FetchPluginsAsync(ct);
        }

        internal async Task<Dictionary<string, PluginInfo>> GetPluginsAsync(CancellationToken ct = default)
        {
            if (_plugins.Count == 0)
                await LoadAsync(ct);

            return _plugins;
        }

        private static async Task<Dictionary<string, PluginInfo>> FetchPluginsAsync(CancellationToken ct)
        {
            var result = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var executablePath = Path.Combine(AppRuntimeState.DatabasePath, "openclaw.cmd");
                if (!File.Exists(executablePath))
                    return result;

                var command = WarpcliHelper.CreateConfiguredCliCommand(executablePath, AppRuntimeState.DatabasePath)
                    .WithArguments(args =>
                    {
                        args.Add("plugins");
                        args.Add("list");
                    })
                    .WithValidation(CommandResultValidation.None);

                var buffered = await command.ExecuteBufferedAsync(ct);
                if (string.IsNullOrWhiteSpace(buffered.StandardOutput))
                    return result;

                ParsePluginTable(buffered.StandardOutput, result);
            }
            catch
            {
                // CLI unavailable or parse failure – return empty
            }

            return result;
        }

        private static void ParsePluginTable(string output, Dictionary<string, PluginInfo> result)
        {
            var lines = output.Split('\n');
            PluginInfo? current = null;
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');

                // Skip separator lines like "+---+---+..."
                if (line.StartsWith('+'))
                    continue;

                // Only process table data lines starting with '|'
                if (!line.StartsWith('|'))
                    continue;

                var columns = line.Split('|');
                // Expected: empty [0], Name [1], ID [2], Status [3], Source [4], Version [5], empty [6]
                if (columns.Length < 6)
                    continue;

                var id = columns[2].Trim();
                var status = columns[3].Trim();

                // Lines with a non-empty ID are the first row of a new plugin entry
                if (!string.IsNullOrEmpty(id))
                {
                    current = new PluginInfo
                    {
                        Id = id,
                        Name = columns[1].Trim(),
                        Status = status,
                        Source = columns[4].Trim(),
                        Version = columns.Length > 5 ? columns[5].Trim() : string.Empty
                    };
                    result[current.Id] = current;
                }
                else if (current is not null)
                {
                    // Continuation row – append Name if present
                    var namePart = columns[1].Trim();
                    if (!string.IsNullOrEmpty(namePart))
                        current.Name = $"{current.Name} {namePart}".Trim();
                }
            }
        }
    }
}
