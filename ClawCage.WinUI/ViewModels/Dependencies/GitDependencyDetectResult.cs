using ClawCage.WinUI.Services.Tools.Helper;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed record GitDependencyDetectResult(
        PortableGitHelper.DetectResult LocalResult,
        bool Installed,
        string CurrentVersion);
}
