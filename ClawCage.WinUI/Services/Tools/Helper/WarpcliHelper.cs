using CliWrap;
using CliWrap.Builders;
using System;

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

        /// <summary>
        /// Creates a command that opens a visible terminal window via <c>cmd.exe /c start "title" /wait ...</c>.
        /// The caller awaits the returned <see cref="Command"/>; it completes when the spawned window closes.
        /// </summary>
        internal static Command CreateVisibleCommand(
            string windowTitle,
            string executablePath,
            Action<ArgumentsBuilder> configureArgs,
            string? workingDirectory = null)
        {
            string newTitle = windowTitle.Replace(" ", "-");
            return CreateConfiguredCliCommand("cmd.exe", workingDirectory)
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
            string? workingDirectory = null)
        {
            return CreateConfiguredCliCommand(executablePath, workingDirectory)
                .WithArguments(configureArgs)
                .WithValidation(CommandResultValidation.None);
        }
    }
}
