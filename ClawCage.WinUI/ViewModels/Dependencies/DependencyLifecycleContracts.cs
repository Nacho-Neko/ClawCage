using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed record DependencyOperationResult<TDetect>(
        bool Success,
        bool Cancelled,
        string? ErrorMessage,
        TDetect Detection);

    internal interface IDependencyLifecycleComponent<TDetect, in TInstallRequest, in TUninstallRequest, in TUpdateRequest>
    {
        Task<TDetect> DetectAsync(string databasePath);
        Task<DependencyOperationResult<TDetect>> InstallAsync(TInstallRequest request);
        Task<DependencyOperationResult<TDetect>> UninstallAsync(TUninstallRequest request);
        Task<DependencyOperationResult<TDetect>> UpdateAsync(TUpdateRequest request);
    }
}
