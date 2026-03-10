using ClawCage.WinUI.Services.Tools.Download;
using CliWrap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Helper
{
    internal static class NodeJsHelper
    {
        internal record DetectResult(bool Found, Version? Version, string? RawOutput);
        internal record CommandResult(bool Success, int ExitCode, string Output, string Error);
        internal record NodeVersionOption(string Version, bool IsLts);

        // Relative sub-directory inside databasePath where the local Node.js runtime lives
        internal const string NodeJsSubDir = "node";
        internal const string NpmRegistry = "https://registry.npmmirror.com";


        // Check Node.js at a specific exe path
        internal static Task<DetectResult> DetectAtPathAsync(string nodeExePath) => DetectCoreAsync(nodeExePath);

        // Returns the node.exe path if ClawCage's own runtime exists inside databasePath.
        // Supported layouts (tried in order):
        //   Flat   : <databasePath>\{{NodeJsSubDir}}\node.exe            (Windows zip, contents moved up)
        //   POSIX  : <databasePath>\{{NodeJsSubDir}}\bin\node.exe        (POSIX / nvm layout)
        //   Zip    : <databasePath>\{{NodeJsSubDir}}\node-vX.Y.Z-win-x64\node.exe  (extracted zip, untouched)
        internal static string? FindLocalNodeExe(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath)) return null;

            var nodeDir = Path.Combine(databasePath, NodeJsSubDir);

            var rootExe = Path.Combine(nodeDir, "node.exe");
            if (File.Exists(rootExe)) return rootExe;

            var binExe = Path.Combine(nodeDir, "bin", "node.exe");
            if (File.Exists(binExe)) return binExe;

            if (Directory.Exists(nodeDir))
            {
                foreach (var sub in Directory.EnumerateDirectories(nodeDir))
                {
                    var subExe = Path.Combine(sub, "node.exe");
                    if (File.Exists(subExe)) return subExe;
                }
            }

            return null;
        }

        internal static bool IsVersionSufficient(Version? version) => version?.Major >= 22;

        internal static async Task<List<NodeVersionOption>> GetOrderedNodeVersionsAsync(CancellationToken ct = default)
        {
            var versions = await NodeJsDownloader.FetchLatestPerMajorAsync(ct);

            static Version ParseVer(string v) =>
                Version.TryParse(v.TrimStart('v', 'V'), out var ver) ? ver : new Version();

            return versions
                .OrderByDescending(v => ParseVer(v.Version))
                .Select(v => new NodeVersionOption(v.Version, v.IsLts))
                .ToList();
        }

        private static Task<DetectResult> DetectCoreAsync(string nodeCommand) => Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(nodeCommand, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi)!;
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                if (Version.TryParse(output.TrimStart('v'), out var version))
                    return new DetectResult(true, version, output);

                return new DetectResult(true, null, output);
            }
            catch
            {
                return new DetectResult(false, null, null);
            }
        });



        // Build the expected zip-extracted directory name for a version tag
        // "v24.1.0" or "24.1.0"  →  "node-v24.1.0-win-x64" (arch-aware)
        internal static string GetVersionDirName(string version)
        {
            var ver = version.StartsWith('v') ? version : "v" + version;
            return $"node-{ver}-win-{NodeJsDownloader.NodeArchSuffix}";
        }

        // Find node.exe for a specific version using the zip-layout path derived from its tag
        internal static string? FindNodeExeForVersion(string databasePath, string version)
        {
            if (string.IsNullOrEmpty(databasePath) || string.IsNullOrEmpty(version)) return null;
            var exe = Path.Combine(databasePath, NodeJsSubDir, GetVersionDirName(version), "node.exe");
            return File.Exists(exe) ? exe : null;
        }


        internal static async Task<CommandResult> NpmInstallAsync(
            string packageName,
            bool globalInstall,
            string? installPath = null,
            CancellationToken cancellationToken = default,
            Action<string>? logCallback = null)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return new CommandResult(false, -1, string.Empty, "包名不能为空。");

            if (!string.IsNullOrWhiteSpace(installPath))
                Directory.CreateDirectory(installPath);


            var databasePath = AppRuntimeState.DatabasePath;
            string nodePath = SecureConfigStore.GetEnvironmentValue("NODE_DIR");

            var command = WarpcliHelper.CreateConfiguredCliCommand(Path.Combine(nodePath, "npm.cmd"), databasePath)
                .WithArguments(args =>
                {
                    args.Add("i");
                    if (globalInstall)
                        args.Add("-g");
                    args.Add(packageName);
                    if (!string.IsNullOrWhiteSpace(installPath))
                        args.Add($"--prefix={installPath}");
                    args.Add($"--registry={NpmRegistry}");
                    args.Add("--loglevel=info");
                    args.Add("--progress=false");
                    args.Add("--no-audit");
                    args.Add("--no-fund");
                })
                .WithValidation(CommandResultValidation.None);

            try
            {
                var result = await command
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(chunk =>
                    {
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            logCallback?.Invoke(chunk);
                        }
                    }))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(chunk =>
                    {
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            logCallback?.Invoke(chunk);
                        }
                    }))
                    .ExecuteAsync(cancellationToken);

                return new CommandResult(result.ExitCode == 0, result.ExitCode, string.Empty, string.Empty);
            }
            catch (OperationCanceledException)
            {
                return new CommandResult(false, -1, string.Empty, "安装已取消。");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, -1, string.Empty, ex.Message);
            }
        }

        internal static async Task<CommandResult> VerifyOpenClawAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            var openClawCmdPath = Path.Combine(databasePath, "openclaw.cmd");
            if (!File.Exists(openClawCmdPath))
                return new CommandResult(false, -1, string.Empty, $"未找到命令: {openClawCmdPath}");

            var command = WarpcliHelper.CreateConfiguredCliCommand(openClawCmdPath, databasePath)
                .WithArguments(args => args.Add("-V"))
                .WithValidation(CommandResultValidation.None);

            try
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                var result = await command
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
                    .ExecuteAsync(cancellationToken);

                return new CommandResult(result.ExitCode == 0, result.ExitCode, stdout.ToString(), stderr.ToString());
            }
            catch (OperationCanceledException)
            {
                return new CommandResult(false, -1, string.Empty, "校验已取消。");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, -1, string.Empty, ex.Message);
            }
        }
    }
}
