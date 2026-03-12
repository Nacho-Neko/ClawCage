using ClawCage.WinUI.Services.Tools.Download;
using ClawCage.WinUI.Services.Tools.Helper;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed class GitDependencyRuntimeComponent
        : IDependencyLifecycleComponent<GitDependencyDetectResult, GitDependencyInstallRequest, GitDependencyUninstallRequest, GitDependencyInstallRequest>
    {
        internal DependencyComponentMetadata Metadata { get; } =
            new("git", "PortableGit", "Git 命令行环境", "\uE7A1", "ms-appx:///Asset/Softs/git.png", "v2.48.1.windows.1", false);

        public async Task<DependencyOperationResult<GitDependencyDetectResult>> InstallAsync(GitDependencyInstallRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DatabasePath))
            {
                var invalidDetect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(false, false, "数据库路径无效。", invalidDetect);
            }

            try
            {
                await PortableGitDownloader.DownloadAndInstallAsync(request.DatabasePath, request.Progress, request.CancellationToken);
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(true, false, null, detect);
            }
            catch (OperationCanceledException)
            {
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(false, true, null, detect);
            }
            catch (Exception ex)
            {
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(false, false, ex.Message, detect);
            }
        }

        public async Task<DependencyOperationResult<GitDependencyDetectResult>> UninstallAsync(GitDependencyUninstallRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DatabasePath))
            {
                var invalidDetect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(false, false, "数据库路径无效。", invalidDetect);
            }

            var gitDir = PortableGitHelper.GetPortableGitDirectory(request.DatabasePath);
            if (!Directory.Exists(gitDir))
            {
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(true, false, null, detect);
            }

            if (request.ConfirmDeleteAsync is not null)
            {
                var confirm = await request.ConfirmDeleteAsync.Invoke("确定要删除 PortableGit？此操作不可撤销。", "卸载 PortableGit");
                if (!confirm)
                {
                    var cancelledDetect = await DetectAsync(request.DatabasePath);
                    return new DependencyOperationResult<GitDependencyDetectResult>(false, true, null, cancelledDetect);
                }
            }

            try
            {
                await Task.Run(() => Directory.Delete(gitDir, true));
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(true, false, null, detect);
            }
            catch (Exception ex)
            {
                var detect = await DetectAsync(request.DatabasePath);
                return new DependencyOperationResult<GitDependencyDetectResult>(false, false, ex.Message, detect);
            }
        }

        public Task<DependencyOperationResult<GitDependencyDetectResult>> UpdateAsync(GitDependencyInstallRequest request)
            => InstallAsync(request);

        public async Task<GitDependencyDetectResult> DetectAsync(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return new GitDependencyDetectResult(new PortableGitHelper.DetectResult(false, null), false, "未检测到");

            var gitExe = PortableGitHelper.FindLocalGitExe(databasePath);
            var localResult = gitExe is not null
                ? await PortableGitHelper.DetectAtPathAsync(gitExe)
                : new PortableGitHelper.DetectResult(false, null);

            if (localResult.Found)
            {
                await PortableGitHelper.ConfigureDefaultIdentityAsync(gitExe, "mekou", "mekou@mekou.net");
                await PortableGitHelper.ConfigureGithubUrlReplacementAsync(gitExe);
            }

            var installed = localResult.Found;
            var currentVersion = installed ? (localResult.RawOutput ?? "已安装") : "未检测到";

            return new GitDependencyDetectResult(localResult, installed, currentVersion);
        }
    }
}
