using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // The three hashes computed for a file, lower-case hex.
    public record ChecksumResult(string Md5, string Sha1, string Sha256);

    // Streaming MD5 / SHA-1 / SHA-256 over a local file in a single pass, plus best-effort
    // reading of a ".sha256" / ".sha1" / ".md5" sidecar sitting next to the file. Not part of
    // IFileSystemService: like ArchiveService, hashing / verification I/O is the documented
    // routing exception. Instance methods; the sync work runs via an internal Task.Run and
    // throws on failure (no message boxes - callers catch and surface).
    public class ChecksumService
    {
        private const int BufferSize = 1024 * 1024;

        // Probed in this order; the first that exists and yields a token wins.
        private static readonly string[] SidecarExtensions = { ".sha256", ".sha1", ".md5" };

        // progress is reported 0..100 against the file length (0 and 100 always fire).
        public Task<ChecksumResult> ComputeAsync(string path, IProgress<int> progress, CancellationToken ct)
        {
            return Task.Run(() => Compute(path, progress, ct), ct);
        }

        private static ChecksumResult Compute(string path, IProgress<int> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            progress.Report(0);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                       BufferSize, FileOptions.SequentialScan))
            {
                long total = stream.Length;
                long processed = 0;
                int lastPercent = -1;
                var buffer = new byte[BufferSize];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    md5.AppendData(buffer, 0, read);
                    sha1.AppendData(buffer, 0, read);
                    sha256.AppendData(buffer, 0, read);
                    processed += read;
                    int percent = total <= 0 ? 100 : (int)(processed * 100 / total);
                    if (percent != lastPercent)
                    {
                        progress.Report(percent);
                        lastPercent = percent;
                    }
                }
            }

            progress.Report(100);
            return new ChecksumResult(
                Convert.ToHexStringLower(md5.GetHashAndReset()),
                Convert.ToHexStringLower(sha1.GetHashAndReset()),
                Convert.ToHexStringLower(sha256.GetHashAndReset()));
        }

        // Looks for "<file>.sha256" / ".sha1" / ".md5" beside the target. Returns the first
        // whitespace-delimited token of the first non-empty line - the usual
        // "<hash> *filename" / "<hash>  filename" sidecar layout - or null if nothing usable
        // is found. A missing / locked / garbage sidecar just means "no pre-fill".
        public async Task<string?> TryReadSidecarAsync(string filePath, CancellationToken ct = default)
        {
            foreach (var ext in SidecarExtensions)
            {
                var sidecarPath = filePath + ext;
                if (!File.Exists(sidecarPath))
                {
                    continue;
                }
                try
                {
                    var lines = await File.ReadAllLinesAsync(sidecarPath, ct);
                    var line = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                    var token = line?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // best effort - fall through to the next candidate extension
                }
            }
            return null;
        }
    }
}
