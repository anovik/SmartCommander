using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // Snapshot of a local file/folder's metadata. Timestamps are local time.
    public record ItemProperties(
        string FullName,
        bool IsFolder,
        bool IsReadOnly,
        bool IsHidden,
        long Size,
        DateTime CreationTime,
        DateTime LastWriteTime,
        DateTime LastAccessTime);

    // Running (or final) tally of a recursive folder walk.
    public record FolderTally(long Files, long Folders, long Bytes);

    // Direct-I/O reads/writes of local file attributes and timestamps, plus a cancellable
    // recursive folder-size walk. Not part of IFileSystemService (same exception as
    // ArchiveService / ChecksumService). Throws on failure; callers catch and surface.
    public class PropertiesService
    {
        public Task<ItemProperties> GetAsync(string path, bool isFolder)
        {
            return Task.Run(() => Get(path, isFolder));
        }

        private static ItemProperties Get(string path, bool isFolder)
        {
            FileSystemInfo info = isFolder ? new DirectoryInfo(path) : new FileInfo(path);
            var attributes = info.Attributes;
            long size = isFolder ? 0 : ((FileInfo)info).Length;
            return new ItemProperties(
                path,
                isFolder,
                attributes.HasFlag(FileAttributes.ReadOnly),
                attributes.HasFlag(FileAttributes.Hidden),
                size,
                info.CreationTime,
                info.LastWriteTime,
                info.LastAccessTime);
        }

        // Applies only the two user-editable flags; every other attribute is left untouched.
        // Hidden is a Windows-only concept here, so it is only written on Windows.
        public Task ApplyAttributesAsync(string path, bool readOnly, bool hidden)
        {
            return Task.Run(() =>
            {
                var attributes = File.GetAttributes(path);
                attributes = readOnly
                    ? attributes | FileAttributes.ReadOnly
                    : attributes & ~FileAttributes.ReadOnly;
                if (OperatingSystem.IsWindows())
                {
                    attributes = hidden
                        ? attributes | FileAttributes.Hidden
                        : attributes & ~FileAttributes.Hidden;
                }
                File.SetAttributes(path, attributes);
            });
        }

        public Task<FolderTally> ComputeFolderTallyAsync(string path, IProgress<FolderTally>? progress, CancellationToken ct)
        {
            return Task.Run(() => Walk(path, progress, ct), ct);
        }

        private static FolderTally Walk(string path, IProgress<FolderTally>? progress, CancellationToken ct)
        {
            long files = 0, folders = 0, bytes = 0;
            var stack = new Stack<string>();
            stack.Push(path);
            var lastReport = DateTime.UtcNow;

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = stack.Pop();

                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directory);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (Directory.Exists(entry))
                    {
                        folders++;
                        // Don't descend into symlinked/junction directories - that risks a cycle.
                        if (!new DirectoryInfo(entry).Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            stack.Push(entry);
                        }
                    }
                    else
                    {
                        files++;
                        try
                        {
                            bytes += new FileInfo(entry).Length;
                        }
                        catch (IOException)
                        {
                            // Best effort - a vanished/locked file just doesn't add to the total.
                        }
                    }

                    var now = DateTime.UtcNow;
                    if (progress != null && (now - lastReport).TotalMilliseconds >= 150)
                    {
                        progress.Report(new FolderTally(files, folders, bytes));
                        lastReport = now;
                    }
                }
            }

            var result = new FolderTally(files, folders, bytes);
            progress?.Report(result);
            return result;
        }
    }
}
