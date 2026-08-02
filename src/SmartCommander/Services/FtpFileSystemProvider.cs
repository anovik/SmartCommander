using FluentFTP;
using Serilog;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // Backed by FluentFTP's AsyncFtpClient. Exactly one instance exists at a time (owned by
    // the composite FileSystemService, created on connect, disposed on disconnect) since only
    // one FTP connection is ever active app-wide. All paths passed into this provider are full
    // "ftp://" strings (see RemotePath); FluentFTP only ever sees the remote part.
    public class FtpFileSystemProvider : IFileSystemProvider, IAsyncDisposable
    {
        private AsyncFtpClient? _client;

        public string? Host { get; private set; }
        public bool IsConnected => _client is { IsConnected: true };

        public async Task ConnectAsync(string host, int port, string username, string password, bool anonymous, CancellationToken ct)
        {
            var client = new AsyncFtpClient(host, anonymous ? "anonymous" : username, anonymous ? "" : password, port);
            try
            {
                await client.AutoConnect(ct);
            }
            catch
            {
                client.Dispose();
                throw;
            }
            _client = client;
            Host = host;
        }

        public async Task DisconnectAsync()
        {
            if (_client == null)
            {
                return;
            }
            await _client.Disconnect();
            _client.Dispose();
            _client = null;
            Host = null;
        }

        public ValueTask DisposeAsync()
        {
            var task = DisconnectAsync();
            return new ValueTask(task);
        }

        private AsyncFtpClient Client =>
            _client ?? throw new InvalidOperationException("Not connected to an FTP server.");

        public async Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, DirectoryListingFilter filter, CancellationToken ct)
        {
            var listing = await Client.GetListing(RemotePath.GetRemotePart(path), ToFtpListOption(filter), ct);
            return listing
                .Where(i => i.Type == FtpObjectType.Directory)
                .Where(i => filter.IncludeHidden || !IsDotfile(i))
                .Select(i => RemotePath.Combine(i.FullName))
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetFilesAsync(string path, DirectoryListingFilter filter, CancellationToken ct)
        {
            var listing = await Client.GetListing(RemotePath.GetRemotePart(path), ToFtpListOption(filter), ct);
            return listing
                .Where(i => i.Type == FtpObjectType.File)
                .Where(i => filter.IncludeHidden || !IsDotfile(i))
                .Select(i => RemotePath.Combine(i.FullName))
                .ToList();
        }

        // FTP has no Windows-style hidden attribute; mirror the Linux dotfile convention
        // LocalFileSystemProvider already applies for IncludeHidden.
        private static bool IsDotfile(FtpListItem item) => item.Name.StartsWith('.');

        private static FtpListOption ToFtpListOption(DirectoryListingFilter filter)
        {
            var options = FtpListOption.Auto;
            if (filter.IncludeHidden)
            {
                options |= FtpListOption.AllFiles;
            }
            if (filter.Recursive)
            {
                options |= FtpListOption.Recursive;
            }
            return options;
        }

        public Task<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
            Client.DirectoryExists(RemotePath.GetRemotePart(path), ct);

        public Task<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
            Client.FileExists(RemotePath.GetRemotePart(path), ct);

        // Pure path math against the ftp:// scheme — no network call needed.
        public Task<string?> GetDirectoryParentAsync(string path, CancellationToken ct = default)
        {
            var trimmed = RemotePath.GetRemotePart(path).TrimEnd('/');
            if (trimmed.Length == 0)
            {
                return Task.FromResult<string?>(null);
            }
            var lastSlash = trimmed.LastIndexOf('/');
            var parent = lastSlash <= 0 ? "/" : trimmed.Substring(0, lastSlash);
            return Task.FromResult<string?>(RemotePath.Combine(parent));
        }

        // FTP has no drive concept; the root is always "/".
        public Task<string?> GetPathRootAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<string?>(RemotePath.Combine("/"));

        public Task<long> GetFileSizeAsync(string path) =>
            Client.GetFileSize(RemotePath.GetRemotePart(path));

        // FTP (via MDTM) only exposes a modified time, not a creation time; used as the
        // closest available approximation for FileViewModel's metadata display.
        public Task<DateTime> GetCreationTimeAsync(string path) =>
            Client.GetModifiedTime(RemotePath.GetRemotePart(path));

        public async Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            long total = 0;
            foreach (var (fullName, isFolder) in items)
            {
                total += isFolder
                    ? await GetDirectorySizeAsync(fullName)
                    : await Client.GetFileSize(RemotePath.GetRemotePart(fullName));
            }
            return total;
        }

        private async Task<long> GetDirectorySizeAsync(string path)
        {
            var listing = await Client.GetListing(RemotePath.GetRemotePart(path), FtpListOption.Recursive, CancellationToken.None);
            return listing.Where(i => i.Type == FtpObjectType.File).Sum(i => i.Size);
        }

        public Task MoveFileAsync(string source, string dest) =>
            Client.MoveFile(RemotePath.GetRemotePart(source), RemotePath.GetRemotePart(dest));

        public Task MoveDirectoryAsync(string source, string dest) =>
            Client.MoveDirectory(RemotePath.GetRemotePart(source), RemotePath.GetRemotePart(dest));

        public Task RenameAsync(string oldPath, string newPath, bool isFolder) =>
            Client.Rename(RemotePath.GetRemotePart(oldPath), RemotePath.GetRemotePart(newPath));

        public Task CreateDirectoryAsync(string path) =>
            Client.CreateDirectory(RemotePath.GetRemotePart(path));

        public Task DeleteFileAsync(string path) =>
            Client.DeleteFile(RemotePath.GetRemotePart(path));

        public Task DeleteDirectoryAsync(string path, CancellationToken ct = default) =>
            Client.DeleteDirectory(RemotePath.GetRemotePart(path), ct);

        // Required by IFileSystemProvider, but unreachable via the composite's dispatch:
        // FTP-to-FTP never occurs since at most one pane is ever FTP. Local<->FTP transfers
        // go through UploadFileAsync/DownloadFileAsync below instead.
        public Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                        IProgress<int>? progress, long processedSize, long totalSize,
                                        CancellationToken ct) =>
            throw new NotSupportedException("FTP-to-FTP transfers are not supported.");

        public Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool delete, bool overwrite,
                                             IProgress<int>? progress, long processedSize, long totalSize,
                                             CancellationToken ct) =>
            throw new NotSupportedException("FTP-to-FTP transfers are not supported.");

        // Called by the composite for a local -> FTP file transfer.
        public async Task<long> UploadFileAsync(string localSource, string ftpDest, bool delete, bool overwrite,
                                                 IProgress<int>? progress, long processedSize, long totalSize,
                                                 CancellationToken ct)
        {
            var fileSize = new FileInfo(localSource).Length;
            var existsMode = overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip;
            var adapter = new FtpProgressAdapter(progress, processedSize, totalSize);
            var status = await Client.UploadFile(localSource, RemotePath.GetRemotePart(ftpDest), existsMode,
                false, FtpVerify.None, adapter, ct);

            long processed = processedSize + (status == FtpStatus.Skipped ? 0 : fileSize);
            ReportProgress(progress, processed, totalSize);

            if (status != FtpStatus.Skipped && delete)
            {
                File.Delete(localSource);
            }
            return processed;
        }

        // Called by the composite for an FTP -> local file transfer.
        public async Task<long> DownloadFileAsync(string ftpSource, string localDest, bool delete, bool overwrite,
                                                   IProgress<int>? progress, long processedSize, long totalSize,
                                                   CancellationToken ct)
        {
            var remoteSource = RemotePath.GetRemotePart(ftpSource);
            var fileSize = Math.Max(0, await Client.GetFileSize(remoteSource, -1, ct));

            if (!overwrite && File.Exists(localDest))
            {
                long skipped = processedSize + fileSize;
                ReportProgress(progress, skipped, totalSize);
                return skipped;
            }

            var adapter = new FtpProgressAdapter(progress, processedSize, totalSize);
            var status = await Client.DownloadFile(localDest, remoteSource, FtpLocalExists.Overwrite,
                FtpVerify.None, adapter, ct);

            long processed = processedSize + fileSize;
            ReportProgress(progress, processed, totalSize);

            if (status == FtpStatus.Success && delete)
            {
                await Client.DeleteFile(remoteSource, ct);
            }
            return processed;
        }

        // Called by the composite for a local -> FTP directory transfer.
        public async Task<long> UploadDirectoryAsync(string localSource, string ftpDest, bool delete, bool overwrite,
                                                      IProgress<int>? progress, long processedSize, long totalSize,
                                                      CancellationToken ct)
        {
            var dirSize = GetLocalDirectorySize(localSource);
            var adapter = new FtpProgressAdapter(progress, processedSize, totalSize, dirSize);
            await Client.UploadDirectory(localSource, RemotePath.GetRemotePart(ftpDest), FtpFolderSyncMode.Update,
                overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip, FtpVerify.None, null, adapter, ct);

            long processed = processedSize + dirSize;
            ReportProgress(progress, processed, totalSize);

            if (delete)
            {
                Directory.Delete(localSource, true);
            }
            return processed;
        }

        // Called by the composite for an FTP -> local directory transfer.
        public async Task<long> DownloadDirectoryAsync(string ftpSource, string localDest, bool delete, bool overwrite,
                                                        IProgress<int>? progress, long processedSize, long totalSize,
                                                        CancellationToken ct)
        {
            var remoteSource = RemotePath.GetRemotePart(ftpSource);
            var dirSize = await GetDirectorySizeAsync(ftpSource);
            var adapter = new FtpProgressAdapter(progress, processedSize, totalSize, dirSize);
            await Client.DownloadDirectory(localDest, remoteSource, FtpFolderSyncMode.Update,
                overwrite ? FtpLocalExists.Overwrite : FtpLocalExists.Skip, FtpVerify.None, null, adapter, ct);

            long processed = processedSize + dirSize;
            ReportProgress(progress, processed, totalSize);

            if (delete)
            {
                await Client.DeleteDirectory(remoteSource, ct);
            }
            return processed;
        }

        public Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                                string contentText, IProgress<string> results,
                                IProgress<string>? statusProgress, CancellationToken ct) =>
            SearchCore(folder, pattern, topOnly, searchContent, contentText, results, statusProgress, ct);

        private async Task SearchCore(string folder, string pattern, bool topOnly, bool searchContent,
                                      string contentText, IProgress<string> results,
                                      IProgress<string>? statusProgress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            statusProgress?.Report(folder);

            FtpListItem[] listing;
            try
            {
                listing = await Client.GetListing(RemotePath.GetRemotePart(folder), FtpListOption.Auto, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FTP search failed in {Folder}", folder);
                return;
            }

            var regex = WildcardToRegex(pattern);
            foreach (var item in listing)
            {
                ct.ThrowIfCancellationRequested();
                if (item.Type == FtpObjectType.File && regex.IsMatch(item.Name))
                {
                    if (searchContent)
                    {
                        if (await FileContainsTextAsync(item.FullName, contentText, ct))
                        {
                            results.Report(RemotePath.Combine(item.FullName));
                        }
                    }
                    else
                    {
                        results.Report(RemotePath.Combine(item.FullName));
                    }
                }
                else if (item.Type == FtpObjectType.Directory && !searchContent && regex.IsMatch(item.Name))
                {
                    results.Report(RemotePath.Combine(item.FullName));
                }
            }

            if (!topOnly)
            {
                foreach (var dir in listing.Where(i => i.Type == FtpObjectType.Directory))
                {
                    ct.ThrowIfCancellationRequested();
                    await SearchCore(RemotePath.Combine(dir.FullName), pattern, topOnly, searchContent, contentText,
                        results, statusProgress, ct);
                }
            }
        }

        private async Task<bool> FileContainsTextAsync(string remotePath, string contentText, CancellationToken ct)
        {
            try
            {
                var bytes = await Client.DownloadBytes(remotePath, ct);
                return bytes != null && Encoding.UTF8.GetString(bytes).Contains(contentText);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Regex WildcardToRegex(string pattern) =>
            new("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase);

        public async Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath)
        {
            var duplicates = new List<string>();
            var destRemote = RemotePath.GetRemotePart(destPath);
            foreach (var (fullName, isFolder) in items)
            {
                var targetRemote = CombineRemote(destRemote, Path.GetFileName(fullName));
                if (isFolder)
                {
                    await CollectDuplicatesInDirectoryAsync(fullName, RemotePath.Combine(targetRemote), duplicates);
                }
                else if (await Client.FileExists(targetRemote))
                {
                    duplicates.Add(fullName);
                }
            }
            return duplicates;
        }

        private async Task CollectDuplicatesInDirectoryAsync(string sourceDir, string destDir, List<string> duplicates)
        {
            if (!await DirectoryExistsAsync(sourceDir))
            {
                return;
            }
            var destRemote = RemotePath.GetRemotePart(destDir);
            var filter = new DirectoryListingFilter();
            foreach (var file in await GetFilesAsync(sourceDir, filter, CancellationToken.None))
            {
                var targetRemote = CombineRemote(destRemote, Path.GetFileName(file));
                if (await Client.FileExists(targetRemote))
                {
                    duplicates.Add(file);
                }
            }
            foreach (var dir in await GetDirectoriesAsync(sourceDir, filter, CancellationToken.None))
            {
                var targetSub = RemotePath.Combine(CombineRemote(destRemote, Path.GetFileName(dir)));
                await CollectDuplicatesInDirectoryAsync(dir, targetSub, duplicates);
            }
        }

        private static string CombineRemote(string basePart, string name) => basePart.TrimEnd('/') + "/" + name;

        public async Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            var result = new List<string>();
            if (!OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty)
            {
                return result;
            }
            foreach (var (fullName, isFolder) in items)
            {
                if (!isFolder)
                {
                    continue;
                }
                var listing = await Client.GetListing(RemotePath.GetRemotePart(fullName), FtpListOption.Auto);
                if (listing.Length > 0)
                {
                    result.Add(fullName);
                }
            }
            return result;
        }

        private static long GetLocalDirectorySize(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }
            long size = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }
            }
            return size;
        }

        private static void ReportProgress(IProgress<int>? progress, long processedSize, long totalSize)
        {
            if (progress == null)
            {
                return;
            }
            progress.Report(totalSize == 0 ? 0 : Convert.ToInt32(processedSize * 100 / totalSize));
        }

        // Adapts FluentFTP's per-item IProgress<FtpProgress> to the cumulative-across-the-whole-
        // operation IProgress<int> (0-100) that FileOperationViewModel expects, matching
        // LocalFileSystemProvider's processedSize/totalSize accumulation contract. Prefers
        // FtpProgress.TransferredBytes (cumulative bytes for this transfer); falls back to
        // Progress% scaled against a known item size (used for directory transfers, where
        // TransferredBytes semantics are less reliable across multiple files).
        private sealed class FtpProgressAdapter : IProgress<FtpProgress>
        {
            private readonly IProgress<int>? _progress;
            private readonly long _baseProcessed;
            private readonly long _totalSize;
            private readonly long? _itemSize;

            public FtpProgressAdapter(IProgress<int>? progress, long baseProcessed, long totalSize, long? itemSize = null)
            {
                _progress = progress;
                _baseProcessed = baseProcessed;
                _totalSize = totalSize;
                _itemSize = itemSize;
            }

            public void Report(FtpProgress value)
            {
                if (_progress == null)
                {
                    return;
                }

                long processed;
                if (value.TransferredBytes >= 0)
                {
                    processed = _baseProcessed + value.TransferredBytes;
                }
                else if (_itemSize is long itemSize && value.Progress >= 0)
                {
                    processed = _baseProcessed + (long)(itemSize * (value.Progress / 100.0));
                }
                else
                {
                    return;
                }

                processed = Math.Min(processed, _totalSize);
                _progress.Report(_totalSize == 0 ? 0 : Convert.ToInt32(processed * 100 / _totalSize));
            }
        }
    }
}
