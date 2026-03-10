using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Download
{
    internal static class Downloader
    {
        internal const int DefaultMaxSegments = 8;
        internal const long DefaultMinSegmentBytes = 2 * 1024 * 1024;

        internal sealed record DownloadTarget(string Url, string TempPath, long Total, bool SupportsRange);

        internal static HttpClient CreateHttpClient(string referrer, string userAgent, string? acceptLanguage = null)
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var h = client.DefaultRequestHeaders;
            h.Add("User-Agent", userAgent);
            h.Add("Accept", "application/octet-stream,*/*;q=0.9");
            if (!string.IsNullOrWhiteSpace(acceptLanguage))
                h.Add("Accept-Language", acceptLanguage);
            h.Referrer = new Uri(referrer);
            return client;
        }

        internal static async Task<DownloadTarget> ProbeFirstAvailableAsync(
            HttpClient http,
            IEnumerable<(string Url, string TempPath)> candidates,
            CancellationToken ct)
        {
            foreach (var (url, tempPath) in candidates)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, url);
                    using var resp = await http.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode)
                        continue;

                    var total = resp.Content.Headers.ContentLength ?? -1;
                    var supportsRange = resp.Headers.AcceptRanges.Contains("bytes", StringComparer.OrdinalIgnoreCase);
                    return new DownloadTarget(url, tempPath, total, supportsRange);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }

            throw new InvalidOperationException("没有可用下载地址。");
        }

        internal static async Task DownloadWithAutoSegmentsAsync(
            HttpClient http,
            DownloadTarget target,
            IProgress<(long Downloaded, long Total)>? progress,
            CancellationToken ct,
            int maxSegments = DefaultMaxSegments,
            long minSegmentBytes = DefaultMinSegmentBytes)
        {
            if (target.Total >= minSegmentBytes * 2 && target.SupportsRange)
                await SegmentedDownloadAsync(http, target.Url, target.TempPath, target.Total, progress, ct, maxSegments, minSegmentBytes);
            else
                await StreamDownloadAsync(http, target.Url, target.TempPath, progress, ct);
        }

        private static async Task StreamDownloadAsync(
            HttpClient http,
            string url,
            string destPath,
            IProgress<(long Downloaded, long Total)>? progress,
            CancellationToken ct)
        {
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            var buf = new byte[65536];
            long downloaded = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                downloaded += n;
                progress?.Report((downloaded, total));
            }
            await dst.FlushAsync(ct);
        }

        private static async Task SegmentedDownloadAsync(
            HttpClient http,
            string url,
            string destPath,
            long totalBytes,
            IProgress<(long Downloaded, long Total)>? progress,
            CancellationToken ct,
            int maxSegments,
            long minSegmentBytes)
        {
            var segmentCount = (int)Math.Clamp(totalBytes / minSegmentBytes, 1, maxSegments);
            var segmentSize = (totalBytes + segmentCount - 1) / segmentCount;

            using var handle = File.OpenHandle(destPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous);
            RandomAccess.SetLength(handle, totalBytes);

            long downloaded = 0;
            var tasks = Enumerable.Range(0, segmentCount).Select(i => Task.Run(async () =>
            {
                var start = (long)i * segmentSize;
                var end = Math.Min(start + segmentSize - 1, totalBytes - 1);

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new RangeHeaderValue(start, end);
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                var buf = new byte[65536];
                long pos = start;
                int n;
                while ((n = await stream.ReadAsync(buf, ct)) > 0)
                {
                    await RandomAccess.WriteAsync(handle, buf.AsMemory(0, n), pos, ct);
                    pos += n;
                    progress?.Report((Interlocked.Add(ref downloaded, n), totalBytes));
                }
            }, ct));

            await Task.WhenAll(tasks);
        }
    }
}
