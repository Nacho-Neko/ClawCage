using ClawCage.WinUI.Services.Tools.Download;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed record NodeDependencyInstallRequest(
        string DatabasePath,
        string SelectedVersion,
        IProgress<NodeJsDownloader.DownloadProgress>? Progress,
        CancellationToken CancellationToken);

    internal sealed record NodeDependencyUninstallRequest(
        string DatabasePath,
        string? SelectedVersion,
        Func<string, string, Task<bool>>? ConfirmDeleteAsync);
}
