using ClawCage.WinUI.Services.Tools.Helper;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Download
{
    internal static class NodeJsDownloader
    {
        private const string IndexUrl = "https://cdn.npmmirror.com/binaries/node/index.json";
        private const string DownloadBase = "https://cdn.npmmirror.com/binaries/node/";
        private static readonly HttpClient Http = Downloader.CreateHttpClient(
            "https://cdn.npmmirror.com/",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "zh-CN,zh;q=0.9,en;q=0.8");

        // ── Version entry from index.json ──────────────────────────────────
        internal class NodeVersionEntry
        {
            [JsonPropertyName("version")] public string Version { get; init; } = "";
            [JsonPropertyName("date")] public string Date { get; init; } = "";
            [JsonPropertyName("lts")] public JsonElement Lts { get; init; }
            [JsonPropertyName("files")] public string[]? Files { get; init; }

            [JsonIgnore] public bool IsLts => Lts.ValueKind == JsonValueKind.String;
        }


        // Node.js Windows architecture string derived from the current process architecture
        internal static string NodeArchSuffix => RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        // ── Progress ───────────────────────────────────────────────────────
        internal enum DownloadPhase { Preparing, Downloading, Extracting, Installing, Completed, Failed }

        internal record DownloadProgress(
            DownloadPhase Phase,
            long BytesDownloaded = 0,
            long TotalBytes = 0,
            string? Message = null
        );

        // ── Fetch the single latest release for each major version (≥ 18) ─
        internal static async Task<NodeVersionEntry[]> FetchLatestPerMajorAsync(CancellationToken ct = default)
        {
            var json = await Http.GetStringAsync(IndexUrl, ct);
            var all = JsonSerializer.Deserialize<NodeVersionEntry[]>(json) ?? [];

            static Version ParseVer(string v) =>
                Version.TryParse(v.TrimStart('v'), out var ver) ? ver : new Version();

            return all
                .Where(v => v.Files?.Contains($"win-{NodeArchSuffix}-zip") == true
                         && ParseVer(v.Version).Major >= 22)
                .GroupBy(v => ParseVer(v.Version).Major)
                .Select(g => g.OrderByDescending(v => ParseVer(v.Version)).First())
                .OrderByDescending(v => ParseVer(v.Version))
                .ToArray();
        }

        // ── Probe: prefer .7z (smaller), fall back to .zip ───────────────
        private static async Task<Downloader.DownloadTarget> ProbeDownloadTargetAsync(
            string version, CancellationToken ct)
        {
            var candidates = new[] { "7z", "zip" }
                .Select(ext =>
                {
                    var name = $"node-{version}-win-{NodeArchSuffix}.{ext}";
                    return (
                        Url: $"{DownloadBase}{version}/{name}",
                        TempPath: Path.Combine(Path.GetTempPath(), $"clawcage_{name}")
                    );
                });

            try
            {
                return await Downloader.ProbeFirstAvailableAsync(Http, candidates, ct);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException($"无法访问 Node.js {version} 的下载地址。");
            }
        }

        // ── Download, extract and install to <databasePath>\nodejs\ ───────
        internal static async Task DownloadAndInstallAsync(
            string version,
            string databasePath,
            IProgress<DownloadProgress> progress,
            CancellationToken ct)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"clawcage_node_{version}");
            Downloader.DownloadTarget? target = null;

            progress.Report(new(DownloadPhase.Preparing, Message: "连接服务器…"));

            try
            {
                // ── Phase 1: Probe best format + server capabilities ────────
                target = await ProbeDownloadTargetAsync(version, ct);

                var dlProgress = new Progress<(long dl, long tot)>(p =>
                    progress.Report(new(DownloadPhase.Downloading, p.dl, p.tot)));

                // ── Phase 2: Download ─────────────────────────────────────
                await Downloader.DownloadWithAutoSegmentsAsync(Http, target, dlProgress, ct);

                // ── Phase 3: Extract with SharpCompress ──────────────────
                var targetDir = Path.Combine(databasePath, NodeJsHelper.NodeJsSubDir);
                progress.Report(new(DownloadPhase.Extracting, target.Total, target.Total));
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
                Directory.CreateDirectory(targetDir);
                await Task.Run(() => ArchiveFactory.WriteToDirectory(
                    target.TempPath, targetDir,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }), ct);

                // ── Phase 4: Verify installation ─────────────────────────
                progress.Report(new(DownloadPhase.Installing, target.Total, target.Total));
                var nodeExe = NodeJsHelper.FindLocalNodeExe(databasePath);
                if (nodeExe is null)
                    throw new InvalidOperationException("Node.js 安装验证失败：未找到 node.exe。");

                progress.Report(new(DownloadPhase.Completed, target.Total, target.Total));
            }
            finally
            {
                try { if (target is not null && File.Exists(target.TempPath)) File.Delete(target.TempPath); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
