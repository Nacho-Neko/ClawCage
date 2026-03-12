using ClawCage.WinUI.Services.Tools.Helper;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Download
{
    internal static class PortableGitDownloader
    {
        internal const string PortableGitMirrorVersion = "v2.48.1.windows.1";
        internal const string PortableGitBaseVersion = "2.48.1";

        private const string DownloadBase = "https://registry.npmmirror.com/-/binary/git-for-windows/";
        private static readonly HttpClient Http = Downloader.CreateHttpClient(
            "https://registry.npmmirror.com/",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        internal enum DownloadPhase { Preparing, Downloading, Extracting, Installing, Completed, Failed }

        internal static string ArchSuffix => RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "64-bit",
            Architecture.X86 => "32-bit",
            Architecture.Arm64 => "arm64",
            _ => "64-bit"
        };

        internal record DownloadProgress(
            DownloadPhase Phase,
            long BytesDownloaded = 0,
            long TotalBytes = 0,
            string? Message = null
        );


        // https://registry.npmmirror.com/-/binary/git-for-windows/v2.48.1.windows.1/PortableGit-2.48.1-arm64.7z.exe
        private static async Task<Downloader.DownloadTarget> ProbeDownloadTargetAsync(CancellationToken ct)
        {
            var candidates = new[] { ".7z.exe", ".7z", ".zip" }
                .Select(ext =>
                {
                    var name = $"PortableGit-{PortableGitBaseVersion}-{ArchSuffix}{ext}";
                    return (
                        Url: $"{DownloadBase}{PortableGitMirrorVersion}/{name}",
                        TempPath: Path.Combine(Path.GetTempPath(), $"clawcage_{name}")
                    );
                });

            try
            {
                return await Downloader.ProbeFirstAvailableAsync(Http, candidates, ct);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException($"无法访问 {PortableGitBaseVersion} 的下载地址。");
            }
        }


        internal static async Task DownloadAndInstallAsync(
            string databasePath,
            IProgress<DownloadProgress> progress,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new InvalidOperationException("数据库路径为空，无法安装 PortableGit。");

            Directory.CreateDirectory(databasePath);
            progress.Report(new(DownloadPhase.Preparing, Message: "连接服务器…"));
            var target = await ProbeDownloadTargetAsync(ct);
            var tempFile = target.TempPath;
            var targetDir = PortableGitHelper.GetPortableGitDirectory(databasePath);

            try
            {
                var dlProgress = new Progress<(long dl, long tot)>(p =>
                    progress.Report(new(DownloadPhase.Downloading, p.dl, p.tot)));
                await Downloader.DownloadWithAutoSegmentsAsync(Http, target, dlProgress, ct);

                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, recursive: true);
                Directory.CreateDirectory(targetDir);


                progress.Report(new(DownloadPhase.Extracting, target.Total, target.Total));

                await ExtractPortableGitArchiveAsync(tempFile, targetDir, ct);

                progress.Report(new(DownloadPhase.Installing, target.Total, target.Total));

                var gitExe = PortableGitHelper.FindLocalGitExe(databasePath);
                if (gitExe is null)
                    throw new InvalidOperationException("PortableGit 安装验证失败：未找到 git.exe。");

                progress.Report(new(DownloadPhase.Completed, target.Total, target.Total));
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        private static async Task ExtractPortableGitArchiveAsync(string archivePath, string targetDir, CancellationToken ct)
        {
            if (archivePath.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase))
            {
                var temp7zPath = Path.Combine(Path.GetTempPath(), $"clawcage_{Guid.NewGuid():N}.7z");
                try
                {
                    await SfxArchiveHelper.ExtractEmbedded7zToTempFileAsync(archivePath, temp7zPath, ct);
                    await Task.Run(() => ArchiveFactory.WriteToDirectory(
                        temp7zPath,
                        targetDir,
                        new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        }), ct);
                }
                finally
                {
                    try { if (File.Exists(temp7zPath)) File.Delete(temp7zPath); } catch { }
                }

                return;
            }

            await Task.Run(() => ArchiveFactory.WriteToDirectory(
                archivePath,
                targetDir,
                new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                }), ct);
        }
    }
}
