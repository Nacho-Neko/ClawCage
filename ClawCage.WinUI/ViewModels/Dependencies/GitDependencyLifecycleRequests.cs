using ClawCage.WinUI.Services.Tools.Download;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed record GitDependencyInstallRequest(
        string DatabasePath,
        IProgress<PortableGitDownloader.DownloadProgress>? Progress,
        CancellationToken CancellationToken);

    internal sealed record GitDependencyUninstallRequest(
        string DatabasePath,
        Func<string, string, Task<bool>>? ConfirmDeleteAsync);
}
