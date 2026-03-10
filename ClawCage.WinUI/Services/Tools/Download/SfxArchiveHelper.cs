using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Tools.Download
{
    internal static class SfxArchiveHelper
    {
        internal static async Task ExtractEmbedded7zToTempFileAsync(string sfxExePath, string temp7zPath, CancellationToken ct)
        {
            var startOffset = await FindSevenZipSignatureOffsetAsync(sfxExePath, ct);
            if (startOffset < 0)
                throw new InvalidOperationException("无法在 .7z.exe 中定位 7z 压缩数据。", null);

            await using var source = new FileStream(sfxExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            source.Seek(startOffset, SeekOrigin.Begin);
            await using var target = new FileStream(temp7zPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(target, ct);
        }

        private static async Task<long> FindSevenZipSignatureOffsetAsync(string filePath, CancellationToken ct)
        {
            var signature = new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = ArrayPool<byte>.Shared.Rent(8192);

            try
            {
                long globalOffset = 0;
                var carry = 0;

                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(carry, buffer.Length - carry), ct);
                    if (read == 0)
                        return -1;

                    var total = carry + read;
                    for (var i = 0; i <= total - signature.Length; i++)
                    {
                        var matched = true;
                        for (var j = 0; j < signature.Length; j++)
                        {
                            if (buffer[i + j] == signature[j])
                                continue;

                            matched = false;
                            break;
                        }

                        if (matched)
                            return globalOffset + i;
                    }

                    carry = Math.Min(signature.Length - 1, total);
                    Array.Copy(buffer, total - carry, buffer, 0, carry);
                    globalOffset += total - carry;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
