using CliWrap;
using CliWrap.Builders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClawCage.WinUI.Services.Tools.Helper
{
    internal static class WarpcliHelper
    {
        internal static Command CreateConfiguredCliCommand(
            string executablePath,
            string? workingDirectory = null)
        {
            var envVars = SecureConfigStore.LoadEnvironmentValues();
            return Cli.Wrap(executablePath)
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariables(builder =>
                {
                    builder.Set(envVars);
                });
        }

        internal static Command CreateSystemCliCommand(
            string executablePath,
            string? workingDirectory = null)
        {
            var envVars = SecureConfigStore.LoadEnvironmentValues();

            var cmd = Cli.Wrap(executablePath);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                cmd = cmd.WithWorkingDirectory(workingDirectory);

            cmd = cmd.WithEnvironmentVariables(builder =>
            {
                foreach (var (key, value) in envVars)
                {
                    if (IsPathLikeKey(key))
                    {
                        var merged = MergePathWithPriority(
                            Environment.GetEnvironmentVariable(key) ?? string.Empty,
                            value);
                        builder.Set(key, merged);
                    }
                    else
                    {
                        builder.Set(key, value);
                    }
                }
            });

            return cmd;
        }

        /// <summary>
        /// Merges a system PATH value with an envVars PATH value.
        /// envVars entries have highest priority and appear first.
        /// System entries whose directory name matches any envVars entry's directory name
        /// (e.g. both contain "node" or "portablegit") are excluded to avoid version conflicts.
        /// </summary>
        internal static string MergePathWithPriority(string systemPath, string envVarsPath)
        {
            var envEntries = envVarsPath
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            if (envEntries.Count == 0)
                return systemPath;

            var toolKeywords = ExtractToolKeywords(envEntries);

            var systemEntries = systemPath
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var entry in envEntries)
            {
                if (seen.Add(entry.TrimEnd('\\', '/')))
                    result.Add(entry);
            }

            foreach (var entry in systemEntries)
            {
                var normalized = entry.TrimEnd('\\', '/');
                if (!seen.Add(normalized))
                    continue;

                if (toolKeywords.Any(kw =>
                        entry.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    continue;

                result.Add(entry);
            }

            return string.Join(";", result);
        }

        /// <summary>
        /// Extracts tool-identifying keywords from envVars PATH entries by collecting
        /// each segment of the path that looks like a tool directory name.
        /// For example, "C:\tools\node-v22.15.0" yields ["node-v22.15.0", "node"].
        /// </summary>
        internal static HashSet<string> ExtractToolKeywords(List<string> envPathEntries)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in envPathEntries)
            {
                var segments = entry.TrimEnd('\\', '/')
                    .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var seg in segments)
                {
                    if (seg.Contains(':') || seg.Equals("bin", StringComparison.OrdinalIgnoreCase)
                                           || seg.Equals("cmd", StringComparison.OrdinalIgnoreCase)
                                           || seg.Equals("usr", StringComparison.OrdinalIgnoreCase)
                                           || seg.Equals("tools", StringComparison.OrdinalIgnoreCase)
                                           || seg.Equals("local", StringComparison.OrdinalIgnoreCase)
                                           || seg.Length <= 2)
                        continue;

                    keywords.Add(seg);

                    var dashIdx = seg.IndexOf('-');
                    if (dashIdx > 0)
                        keywords.Add(seg[..dashIdx]);
                }
            }

            return keywords;
        }

        private static bool IsPathLikeKey(string key)
            => key.Contains("PATH", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a command that opens a visible terminal window via <c>cmd.exe /c start "title" /wait ...</c>.
        /// The caller awaits the returned <see cref="Command"/>; it completes when the spawned window closes.
        /// </summary>
        internal static Command CreateVisibleCommand(
            string windowTitle,
            string executablePath,
            Action<ArgumentsBuilder> configureArgs,
            string? workingDirectory = null,
            bool useSystemEnv = false)
        {
            string newTitle = windowTitle.Replace(" ", "-");
            var baseCmd = useSystemEnv
                ? CreateSystemCliCommand("cmd.exe", workingDirectory)
                : CreateConfiguredCliCommand("cmd.exe", workingDirectory);
            return baseCmd
                .WithArguments(args =>
                {
                    args.Add("/c");
                    args.Add("start");
                    args.Add($"\"{newTitle}\"");
                    args.Add("/wait");
                    args.Add(executablePath);
                    configureArgs(args);
                })
                .WithValidation(CommandResultValidation.None);
        }

        /// <summary>
        /// Creates a background command (no visible window). This is equivalent to
        /// <see cref="CreateConfiguredCliCommand"/> with <see cref="CommandResultValidation.None"/>.
        /// </summary>
        internal static Command CreateBackgroundCommand(
            string executablePath,
            Action<ArgumentsBuilder> configureArgs,
            string? workingDirectory = null,
            bool useSystemEnv = false)
        {
            var baseCmd = useSystemEnv
                ? CreateSystemCliCommand(executablePath, workingDirectory)
                : CreateConfiguredCliCommand(executablePath, workingDirectory);
            return baseCmd
                .WithArguments(configureArgs)
                .WithValidation(CommandResultValidation.None);
        }
    }
}
