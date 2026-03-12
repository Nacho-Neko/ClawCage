using ClawCage.WinUI.Services.Tools.Helper;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed record NodeDependencyDetectResult(
        NodeJsHelper.DetectResult LocalResult,
        bool Installed,
        bool VersionSufficient,
        bool SelectedReady,
        string CurrentVersion);
}
