using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace SmartCommander.Services
{
    // Deflate levels behind the 4 UI presets (Store / Fastest / Normal / Maximum).
    public enum ZipCompressionLevel
    {
        Store = 0,
        Fastest = 1,
        Normal = 6,
        Maximum = 9
    }

    // Thrown by ExtractZipAsync when the archive is encrypted and the supplied
    // password is wrong (or missing). The pre-launch VerifyPassword loop normally
    // catches the wrong-password case before extraction starts; this covers the
    // surprise case (e.g. a mixed-key archive).
    public class InvalidPasswordException : Exception
    {
        public InvalidPasswordException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }

    // Zip create/extract, both plain and AES-256 encrypted, through a single
    // SharpZipLib code path (plain = Password left unset). Not part of
    // IFileSystemService: archive I/O is the documented routing exception.
    // Instance methods; throws on failure (no message boxes - callers catch and
    // surface). Directly unit-testable, like LocalFileSystemProviderTests.
    public class ArchiveService
    {
        private const int BufferSize = 81920;

        // BFS over the selection (same walk as the old ZipCore). password null/empty => no encryption.
        // progress is reported 0..100 against totalSize (sum of the source file lengths).
        public Task CreateZipAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string zipPath,
            ZipCompressionLevel level, string? password, long totalSize,
            IProgress<int> progress, CancellationToken ct)
        {
            return Task.Run(() => CreateZip(items, zipPath, level, password, totalSize, progress, ct), ct);
        }

        // Entry-by-entry extract, keeps the zip-slip guard. Throws InvalidPasswordException on bad password.
        // progress is reported 0..100 by entry count.
        public Task ExtractZipAsync(string zipPath, string destDir, string? password,
            IProgress<int> progress, CancellationToken ct)
        {
            return Task.Run(() => ExtractZip(zipPath, destDir, password, progress, ct), ct);
        }

        // Quick central-directory scan: true if any file entry is encrypted.
        public bool IsEncrypted(string zipPath)
        {
            using var zf = new ZipFile(zipPath);
            return zf.Cast<ZipEntry>().Any(e => e.IsFile && e.IsCrypted);
        }

        // Open the first encrypted file entry and read one byte (AES authentication
        // happens on read init). Returns false on ZipException, true on success.
        // Used by the pre-launch retry loop.
        public bool VerifyPassword(string zipPath, string password)
        {
            try
            {
                using var zf = new ZipFile(zipPath) { Password = password };
                var entry = zf.Cast<ZipEntry>().FirstOrDefault(e => e.IsFile && e.IsCrypted);
                if (entry == null)
                {
                    return true;
                }

                using var stream = zf.GetInputStream(entry);
                stream.ReadByte();
                return true;
            }
            catch (ZipException)
            {
                return false;
            }
        }

        private void CreateZip(IReadOnlyList<(string FullName, bool IsFolder)> items, string zipPath,
            ZipCompressionLevel level, string? password, long totalSize,
            IProgress<int> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (items.Count < 1)
            {
                return;
            }

            bool encrypted = !string.IsNullOrEmpty(password);

            // CreateNew, not a truncating open: if anything raced us onto this path since the
            // caller's File.Exists check, fail here instead of clobbering it. Opened before the
            // try/catch so the cleanup only ever removes a file this call actually created.
            var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            try
            {
                progress.Report(0);
                long processedSize = 0;

                // (entryPrefix, sourcePath) work queue - mirrors the old ZipCore BFS.
                var queue = new Queue<(string Prefix, string Path)>();
                foreach (var item in items)
                {
                    queue.Enqueue(("", item.FullName));
                }

                using (stream)
                using (var zip = new ZipOutputStream(stream))
                {
                    zip.SetLevel((int)level);
                    if (encrypted)
                    {
                        zip.Password = password;
                    }

                    var buffer = new byte[BufferSize];
                    while (queue.Count > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        var (prefix, path) = queue.Dequeue();

                        if (Directory.Exists(path))
                        {
                            var childPrefix = CombineEntry(prefix, new DirectoryInfo(path).Name);
                            foreach (var folder in Directory.GetDirectories(path))
                            {
                                queue.Enqueue((childPrefix, folder));
                            }
                            foreach (var file in Directory.GetFiles(path))
                            {
                                queue.Enqueue((childPrefix, file));
                            }
                        }
                        else if (File.Exists(path))
                        {
                            var info = new FileInfo(path);
                            var entry = new ZipEntry(ZipEntry.CleanName(CombineEntry(prefix, info.Name)))
                            {
                                DateTime = info.LastWriteTime,
                                Size = info.Length,
                                AESKeySize = encrypted ? 256 : 0
                            };
                            zip.PutNextEntry(entry);

                            using (var source = File.OpenRead(path))
                            {
                                int read;
                                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    zip.Write(buffer, 0, read);
                                    processedSize += read;
                                    progress.Report(totalSize <= 0 ? 0 : (int)(processedSize * 100 / totalSize));
                                }
                            }

                            zip.CloseEntry();
                        }
                    }

                    zip.Finish();
                }

                progress.Report(100);
            }
            catch
            {
                // A partial zip (cancelled or failed mid-write) is unusable and would
                // block a retry via the caller's "archive exists" pre-check. Remove it.
                TryDelete(zipPath);
                throw;
            }
        }

        private void ExtractZip(string zipPath, string destDir, string? password,
            IProgress<int> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            progress.Report(0);
            Directory.CreateDirectory(destDir);
            string destDirFull = Path.GetFullPath(destDir + Path.DirectorySeparatorChar);

            using var zip = new ZipFile(zipPath) { Password = string.IsNullOrEmpty(password) ? null : password };
            var entries = zip.Cast<ZipEntry>().ToList();
            int total = entries.Count;
            int done = 0;
            var buffer = new byte[BufferSize];

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                string destPath = Path.GetFullPath(Path.Combine(destDir, entry.Name));
                if (!destPath.StartsWith(destDirFull, StringComparison.Ordinal))
                {
                    throw new IOException($"Zip entry is outside the target directory: {entry.Name}");
                }

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(destPath);
                }
                else if (entry.IsFile)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    try
                    {
                        using var input = zip.GetInputStream(entry);
                        using var output = File.Create(destPath);
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, read);
                        }
                    }
                    catch (ZipException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidPasswordException(ex.Message, ex);
                    }
                }

                done++;
                progress.Report(total == 0 ? 100 : done * 100 / total);
            }
        }

        // Zip entry paths always use '/'. Path.Combine would inject '\' on Windows.
        private static string CombineEntry(string prefix, string name)
        {
            return string.IsNullOrEmpty(prefix) ? name : prefix + "/" + name;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best effort
            }
        }
    }
}
