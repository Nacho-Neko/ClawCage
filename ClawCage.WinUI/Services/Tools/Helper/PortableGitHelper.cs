using System.Diagnostics;
using System.IO;
using System;
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

        internal static Task<DetectResult> DetectAtPathAsync(string gitExePath) => Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(gitExePath, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi)!;
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                return new DetectResult(process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output),
                    string.IsNullOrWhiteSpace(output) ? null : output);
            }
            catch
            {
                return new DetectResult(false, null);
            }
        });

        internal static async Task<ConfigureResult> ConfigureDefaultIdentityAsync(string databasePath, string userName, string userEmail)
        {
            var gitExePath = FindLocalGitExe(databasePath);
            if (string.IsNullOrWhiteSpace(gitExePath))
                return new ConfigureResult(false, -1, "未找到 portablegit 的 git.exe。");

            if (string.IsNullOrWhiteSpace(userName))
                return new ConfigureResult(false, -1, "user.name 不能为空。");

            if (string.IsNullOrWhiteSpace(userEmail))
                return new ConfigureResult(false, -1, "user.email 不能为空。");

            var currentName = await GetGitConfigValueAsync(gitExePath, databasePath, "user.name");
            if (!string.Equals(currentName, userName, StringComparison.Ordinal))
            {
                var setNameResult = await RunGitConfigAsync(gitExePath, databasePath, "user.name", userName);
                if (!setNameResult.Success)
                    return setNameResult;
            }

            var currentEmail = await GetGitConfigValueAsync(gitExePath, databasePath, "user.email");
            if (!string.Equals(currentEmail, userEmail, StringComparison.Ordinal))
                return await RunGitConfigAsync(gitExePath, databasePath, "user.email", userEmail);

            return new ConfigureResult(true, 0, null);
        }

        private static async Task<string?> GetGitConfigValueAsync(string gitExePath, string databasePath, string key)
        {
            try
            {
                var portableGitHome = GetPortableGitDirectory(databasePath);
                Directory.CreateDirectory(portableGitHome);
                var gitConfigPath = GetPortableGitConfigPath(databasePath);

                var psi = new ProcessStartInfo(gitExePath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("config");
                psi.ArgumentList.Add("--file");
                psi.ArgumentList.Add(gitConfigPath);
                psi.ArgumentList.Add("--get");
                psi.ArgumentList.Add(key);

                using var process = Process.Start(psi)!;
                var stdout = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    return null;

                var value = stdout.Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<ConfigureResult> RunGitConfigAsync(string gitExePath, string databasePath, string key, string value)
        {
            try
            {
                var portableGitHome = GetPortableGitDirectory(databasePath);
                Directory.CreateDirectory(portableGitHome);
                var gitConfigPath = GetPortableGitConfigPath(databasePath);

                var psi = new ProcessStartInfo(gitExePath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("config");
                psi.ArgumentList.Add("--file");
                psi.ArgumentList.Add(gitConfigPath);
                psi.ArgumentList.Add("--replace-all");
                psi.ArgumentList.Add(key);
                psi.ArgumentList.Add(value);

                using var process = Process.Start(psi)!;
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0
                    ? new ConfigureResult(true, process.ExitCode, null)
                    : new ConfigureResult(false, process.ExitCode, string.IsNullOrWhiteSpace(stderr) ? "设置 Git 配置失败。" : stderr.Trim());
            }
            catch (System.Exception ex)
            {
                return new ConfigureResult(false, -1, ex.Message);
            }
        }
    }
}
