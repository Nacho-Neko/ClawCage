using CliWrap;

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
    }
}
