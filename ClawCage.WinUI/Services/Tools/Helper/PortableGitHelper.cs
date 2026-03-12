using CliWrap;
using CliWrap.Buffered;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Helper
{
    internal static class PortableGitHelper
    {
        internal const string PortableGitSubDir = "portablegit";

        internal record DetectResult(bool Found, string? RawOutput);
        internal record ConfigureResult(bool Success, int ExitCode, string? Error);

        internal static string GetPortableGitDirectory(string databasePath)
        {
            return Path.Combine(databasePath ?? string.Empty, PortableGitSubDir);
        }

        internal static string GetPortableGitConfigPath(string databasePath)
        {
            return Path.Combine(GetPortableGitDirectory(databasePath), ".gitconfig");
        }

        internal static string? FindLocalGitExe(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return null;

            var gitDir = GetPortableGitDirectory(databasePath);
            var cmdExe = Path.Combine(gitDir, "cmd", "git.exe");
            if (File.Exists(cmdExe))
                return cmdExe;

            var binExe = Path.Combine(gitDir, "bin", "git.exe");
            return File.Exists(binExe) ? binExe : null;
        }

        internal static Task<DetectResult> DetectAtPathAsync(string gitExePath) => Task.Run(async () =>
        {
            try
            {
                var result = await WarpcliHelper.CreateConfiguredCliCommand(gitExePath)
                    .WithArguments("--version")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                var output = result.StandardOutput.Trim();
                return new DetectResult(result.ExitCode == 0 && !string.IsNullOrWhiteSpace(output),
                     string.IsNullOrWhiteSpace(output) ? null : output);
            }
            catch
            {
                return new DetectResult(false, null);
            }
        });

        internal static async Task<ConfigureResult> ConfigureDefaultIdentityAsync(string gitExePath, string userName, string userEmail)
        {
            var portableGitRoot = GetPortableGitRootFromGitExe(gitExePath);
            if (portableGitRoot is null)
                return new ConfigureResult(false, -1, "无法从 git.exe 路径解析 portablegit 根目录。");

            var configPath = Path.Combine(portableGitRoot, ".gitconfig");


            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(userEmail))
                return new ConfigureResult(false, -1, "user.name 和 user.email 不能为空。");

            // command mode:
            // git config user.name "..."
            // git config user.email "..."
            var setNameResult = await ExecuteGitCommandAsync(gitExePath, new[] { "config", "--file", configPath, "--replace-all", "user.name", userName });
            if (!setNameResult.Success)
                return setNameResult;

            var setEmailResult = await ExecuteGitCommandAsync(gitExePath, new[] { "config", "--file", configPath, "--replace-all", "user.email", userEmail });
            if (!setEmailResult.Success)
                return setEmailResult;

            return new ConfigureResult(true, 0, null);
        }

        internal static async Task<ConfigureResult> ConfigureGithubUrlReplacementAsync(string gitExePath)
        {
            var portableGitRoot = GetPortableGitRootFromGitExe(gitExePath);
            if (portableGitRoot is null)
                return new ConfigureResult(false, -1, "无法从 git.exe 路径解析 portablegit 根目录。");

            var configPath = Path.Combine(portableGitRoot, ".gitconfig");

            // 清理旧值（不存在时忽略结果）
            _ = await ExecuteGitCommandAsync(gitExePath, new[] { "config", "--file", configPath, "--unset-all", "url.https://github.com/.insteadOf" });

            // Config 1: url."https://github.com/".insteadOf "ssh://git@github.com/"
            var res1 = await ExecuteGitCommandAsync(gitExePath, new[] { "config", "--file", configPath, "--add", "url.https://github.com/.insteadOf", "ssh://git@github.com/" });
            if (!res1.Success) return res1;

            // Config 2: url."https://github.com/".insteadOf "git@github.com:"
            var res2 = await ExecuteGitCommandAsync(gitExePath, new[] { "config", "--file", configPath, "--add", "url.https://github.com/.insteadOf", "git@github.com:" });
            if (!res2.Success) return res2;

            return new ConfigureResult(true, 0, null);
        }

        internal static async Task<ConfigureResult> ConfigureDisableCertificateCheckAsync(string gitExePath)
        {
            var portableGitRoot = GetPortableGitRootFromGitExe(gitExePath);
            if (portableGitRoot is null)
                return new ConfigureResult(false, -1, "无法从 git.exe 路径解析 portablegit 根目录。");

            var configPath = Path.Combine(portableGitRoot, ".gitconfig");

            // 等价于：git config http.sslVerify false
            return await ExecuteGitCommandAsync(gitExePath, new[]
            {
                "config", "--file", configPath, "--replace-all", "http.sslVerify", "false"
            });
        }

        private static async Task<ConfigureResult> ExecuteGitCommandAsync(string gitExePath, string[] args)
        {
            try
            {
                var result = await WarpcliHelper.CreateConfiguredCliCommand(gitExePath)
                    .WithArguments(args)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                return result.ExitCode == 0
                    ? new ConfigureResult(true, result.ExitCode, null)
                    : new ConfigureResult(false, result.ExitCode, string.IsNullOrWhiteSpace(result.StandardError) ? "执行 Git 命令失败。" : result.StandardError.Trim());
            }
            catch (Exception ex)
            {
                return new ConfigureResult(false, -1, ex.Message);
            }
        }

        private static string? GetPortableGitRootFromGitExe(string gitExePath)
        {
            if (string.IsNullOrWhiteSpace(gitExePath))
                return null;

            var gitDir = Path.GetDirectoryName(gitExePath);
            if (string.IsNullOrWhiteSpace(gitDir))
                return null;

            var parent = Path.GetDirectoryName(gitDir);
            return string.IsNullOrWhiteSpace(parent) ? null : parent;
        }
    }
}
