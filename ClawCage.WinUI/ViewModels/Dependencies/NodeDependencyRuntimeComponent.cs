using ClawCage.WinUI.Services.Tools.Download;
using ClawCage.WinUI.Services.Tools.Helper;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClawCage.WinUI.ViewModels
{
    internal sealed class NodeDependencyRuntimeComponent
        : IDependencyLifecycleComponent<NodeDependencyDetectResult, NodeDependencyInstallRequest, NodeDependencyUninstallRequest, NodeDependencyInstallRequest>
    {
        internal DependencyComponentMetadata Metadata { get; } =
            new("node", "Node.js", "JavaScript 运行时", "\uE756", "ms-appx:///Asset/Softs/nodejs.png", string.Empty, true);

        public async Task<DependencyOperationResult<NodeDependencyDetectResult>> InstallAsync(NodeDependencyInstallRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DatabasePath) || string.IsNullOrWhiteSpace(request.SelectedVersion))
            {
                var invalidDetect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(false, false, "安装参数无效。", invalidDetect);
            }

            try
            {
                await NodeJsDownloader.DownloadAndInstallAsync(
                    request.SelectedVersion,
                    request.DatabasePath,
                    request.Progress,
                    request.CancellationToken);

                var detect = await DetectWithRetryAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(true, false, null, detect);
            }
            catch (OperationCanceledException)
            {
                var detect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(false, true, null, detect);
            }
            catch (Exception ex)
            {
                var detect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(false, false, ex.Message, detect);
            }
        }

        public async Task<DependencyOperationResult<NodeDependencyDetectResult>> UninstallAsync(NodeDependencyUninstallRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DatabasePath))
            {
                var invalidDetect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(false, false, "数据库路径无效。", invalidDetect);
            }

            var localExe = NodeJsHelper.FindLocalNodeExe(request.DatabasePath);
            if (localExe is null)
            {
                var detect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(true, false, null, detect);
            }

            var versionDir = Path.GetDirectoryName(localExe)!;
            var nodejsDir = Path.Combine(request.DatabasePath, NodeJsHelper.NodeJsSubDir);
            var deleteDir = string.Equals(versionDir, nodejsDir, StringComparison.OrdinalIgnoreCase)
                ? nodejsDir
                : versionDir;
            var dirName = Path.GetFileName(deleteDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (request.ConfirmDeleteAsync is not null)
            {
                var confirm = await request.ConfirmDeleteAsync.Invoke($"确定要删除 {dirName}？此操作不可撤销。", "卸载 Node.js");
                if (!confirm)
                {
                    var cancelledDetect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                    return new DependencyOperationResult<NodeDependencyDetectResult>(false, true, null, cancelledDetect);
                }
            }

            try
            {
                await Task.Run(() => Directory.Delete(deleteDir, true));
                var detect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(true, false, null, detect);
            }
            catch (Exception ex)
            {
                var detect = await DetectAsync(request.DatabasePath, request.SelectedVersion);
                return new DependencyOperationResult<NodeDependencyDetectResult>(false, false, ex.Message, detect);
            }
        }

        public Task<DependencyOperationResult<NodeDependencyDetectResult>> UpdateAsync(NodeDependencyInstallRequest request)
            => InstallAsync(request);

        public Task<NodeDependencyDetectResult> DetectAsync(string databasePath)
            => DetectCoreAsync(databasePath, null);

        internal Task<NodeDependencyDetectResult> DetectAsync(string databasePath, string? selectedVersion)
            => DetectCoreAsync(databasePath, selectedVersion);

        private async Task<NodeDependencyDetectResult> DetectCoreAsync(string databasePath, string? selectedVersion)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return new NodeDependencyDetectResult(new NodeJsHelper.DetectResult(false, null, null), false, false, false, "未检测到");

            string? localExe = !string.IsNullOrEmpty(selectedVersion)
                ? NodeJsHelper.FindNodeExeForVersion(databasePath, selectedVersion)
                : NodeJsHelper.FindLocalNodeExe(databasePath);

            var localResult = localExe is not null
                ? await NodeJsHelper.DetectAtPathAsync(localExe)
                : new NodeJsHelper.DetectResult(false, null, null);

            var installed = localResult.Found && localResult.Version is not null;
            var sufficient = NodeJsHelper.IsVersionSufficient(localResult.Version);
            var selectedReady = installed && IsSelectedVersionMatched(selectedVersion, localResult.Version);
            var currentVersion = installed ? (localResult.RawOutput ?? "已安装") : "未检测到";

            return new NodeDependencyDetectResult(localResult, installed, sufficient, selectedReady, currentVersion);
        }

        private async Task<NodeDependencyDetectResult> DetectWithRetryAsync(string databasePath, string? selectedVersion, int maxAttempts = 3, int delayMs = 800)
        {
            NodeDependencyDetectResult latest = await DetectAsync(databasePath, selectedVersion);
            if (latest.SelectedReady)
                return latest;

            for (int i = 1; i < maxAttempts; i++)
            {
                await Task.Delay(delayMs);
                latest = await DetectAsync(databasePath, selectedVersion);
                if (latest.SelectedReady)
                    break;
            }

            return latest;
        }

        private static bool IsSelectedVersionMatched(string? selectedVersionTag, Version? detectedVersion)
        {
            if (string.IsNullOrWhiteSpace(selectedVersionTag) || detectedVersion is null)
                return false;

            if (!Version.TryParse(selectedVersionTag.TrimStart('v', 'V'), out var selectedVersion))
                return false;

            return selectedVersion == detectedVersion;
        }
    }
}
