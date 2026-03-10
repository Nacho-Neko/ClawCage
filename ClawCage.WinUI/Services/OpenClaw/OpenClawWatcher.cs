using ClawCage.WinUI.Services.Tools.Helper;
using CliWrap;
using CliWrap.Buffered;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.OpenClaw
{
    internal static class OpenClawWatcher
    {
        internal record CommandResult(bool Success, int ExitCode, string Output, string Error);

        private static readonly object SyncRoot = new();

        private static CancellationTokenSource? _runCts;
        private static Task? _runTask;
        private static string? _databasePath;

        internal static event EventHandler? RunningStateChanged;

        internal static bool IsRunning
        {
            get
            {
                lock (SyncRoot)
                    return _runTask is { IsCompleted: false };
            }
        }

        internal static async Task<CommandResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var command = WarpcliHelper.CreateConfiguredCliCommand(Path.Combine(AppRuntimeState.DatabasePath, "openclaw.cmd"), AppRuntimeState.DatabasePath)
                    .WithArguments(args =>
                    {
                        args.Add("onboard");
                        args.Add("--non-interactive");
                        args.Add("--accept-risk");
                    })
                    .WithValidation(CommandResultValidation.None);

                var result = await command.ExecuteBufferedAsync(cancellationToken);
                var isSuccess = result.ExitCode == 0;
                var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                return new CommandResult(isSuccess, result.ExitCode, result.StandardOutput, error);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, -1, string.Empty, ex.Message);
            }
        }

        internal static Task<CommandResult> StartAsync(CancellationToken cancellationToken = default, bool? useVisibleWindow = null)
        {
            var runMode = AppSettings.GetString(AppSettingKeys.RunMode) ?? "gateway";
            var isNodeMode = runMode == "node";

            lock (SyncRoot)
            {
                if (_runTask is { IsCompleted: false })
                    return Task.FromResult(new CommandResult(true, 0, "OpenClaw 已在运行。", string.Empty));

                var runWithVisibleWindow = useVisibleWindow ?? AppSettings.GetBool(AppSettingKeys.OpenClawVisibleWindow, true);
                _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runTask = RunCoreAsync(runWithVisibleWindow, _runCts.Token);
            }

            RunningStateChanged?.Invoke(null, EventArgs.Empty);

            return Task.FromResult(new CommandResult(true, 0, "OpenClaw 启动中。", string.Empty));
        }

        internal static async Task<CommandResult> StopAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokenSource? runCts;
            Task? runTask;

            lock (SyncRoot)
            {
                runCts = _runCts;
                runTask = _runTask;
            }

            if (runTask is null || runTask.IsCompleted)
                return new CommandResult(true, 0, "OpenClaw 未在运行。", string.Empty);

            runCts?.Cancel();

            try
            {
                await runTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new CommandResult(false, -1, string.Empty, "停止已取消。");
            }

            return new CommandResult(true, 0, "OpenClaw 已停止。", string.Empty);
        }

        internal static async Task<CommandResult> RestartAsync(string? databasePath, CancellationToken cancellationToken = default, bool? useVisibleWindow = null)
        {
            var stopResult = await StopAsync(cancellationToken);
            if (!stopResult.Success)
                return stopResult;

            var targetDatabasePath = !string.IsNullOrWhiteSpace(databasePath)
                ? databasePath
                : GetCurrentDatabasePath();

            if (string.IsNullOrWhiteSpace(targetDatabasePath))
                return new CommandResult(false, -1, string.Empty, "未配置 DatabasePath。");

            await Task.Delay(300, cancellationToken);
            return await StartAsync(cancellationToken, useVisibleWindow);
        }

        private static async Task RunCoreAsync(bool useVisibleWindow, CancellationToken cancellationToken)
        {
            var runMode = AppSettings.GetString(AppSettingKeys.RunMode) ?? "gateway";
            try
            {
                var command = useVisibleWindow
                    ? WarpcliHelper.CreateConfiguredCliCommand("cmd.exe", AppRuntimeState.DatabasePath)
                        .WithArguments(args =>
                        {
                            args.Add("/c");
                            args.Add("start");
                            args.Add("\"OpenClaw\"");
                            args.Add("/wait");
                            args.Add("OpenClaw");
                            args.Add(runMode);
                        })
                        .WithValidation(CommandResultValidation.None)
                    : WarpcliHelper.CreateConfiguredCliCommand(Path.Combine(AppRuntimeState.DatabasePath, "openclaw.cmd"), AppRuntimeState.DatabasePath)
                        .WithArguments(args => args.Add(runMode))
                        .WithValidation(CommandResultValidation.None);

                _ = await command.ExecuteAsync(cancellationToken);
            }
            catch
            {
            }
            finally
            {
                lock (SyncRoot)
                {
                    _runCts?.Dispose();
                    _runCts = null;
                    _runTask = null;
                }

                RunningStateChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        private static string? GetCurrentDatabasePath()
        {
            lock (SyncRoot)
                return _databasePath ?? AppRuntimeState.DatabasePath;
        }
    }
}
