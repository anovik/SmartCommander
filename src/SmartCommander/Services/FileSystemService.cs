using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // Composite the ViewModels talk to: dispatches each call to the provider that
    // owns the path (scheme-based, see RemotePath). At most one FTP connection is
    // ever active app-wide, so _ftpProvider is a single nullable field rather than
    // a keyed collection.
    public class FileSystemService : IFileSystemService
    {
        private readonly IFileSystemProvider _local;
        private FtpFileSystemProvider? _ftpProvider;

        public FileSystemService(IFileSystemProvider local)
        {
            _local = local;
        }

        public bool IsFtpConnected => _ftpProvider?.IsConnected == true;
        public string? FtpHost => _ftpProvider?.Host;

        public async Task ConnectFtpAsync(string host, int port, string username, string password, bool anonymous, CancellationToken ct)
        {
            if (_ftpProvider != null)
            {
                await DisconnectFtpAsync();
            }
            var provider = new FtpFileSystemProvider();
            await provider.ConnectAsync(host, port, username, password, anonymous, ct);
            _ftpProvider = provider;
        }

        public async Task DisconnectFtpAsync()
        {
            if (_ftpProvider == null)
            {
                return;
            }
            await _ftpProvider.DisposeAsync();
            _ftpProvider = null;
        }

        private IFileSystemProvider ProviderFor(string path) =>
            RemotePath.IsFtp(path)
                ? (_ftpProvider ?? throw new InvalidOperationException("No FTP connection active."))
                : _local;

        private FtpFileSystemProvider Ftp =>
            _ftpProvider ?? throw new InvalidOperationException("No FTP connection active.");

        public Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, DirectoryListingFilter filter, CancellationToken ct) =>
            ProviderFor(path).GetDirectoriesAsync(path, filter, ct);

        public Task<IReadOnlyList<string>> GetFilesAsync(string path, DirectoryListingFilter filter, CancellationToken ct) =>
            ProviderFor(path).GetFilesAsync(path, filter, ct);

        public Task<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
            ProviderFor(path).DirectoryExistsAsync(path, ct);

        public Task<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
            ProviderFor(path).FileExistsAsync(path, ct);

        public Task<string?> GetDirectoryParentAsync(string path, CancellationToken ct = default) =>
            ProviderFor(path).GetDirectoryParentAsync(path, ct);

        public Task<string?> GetPathRootAsync(string path, CancellationToken ct = default) =>
            ProviderFor(path).GetPathRootAsync(path, ct);

        public Task<long> GetFileSizeAsync(string path) =>
            ProviderFor(path).GetFileSizeAsync(path);

        public Task<DateTime> GetCreationTimeAsync(string path) =>
            ProviderFor(path).GetCreationTimeAsync(path);

        public Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            if (items.Count == 0)
            {
                return Task.FromResult(0L);
            }
            // Items always come from a single pane's selection, so they share one provider.
            return ProviderFor(items[0].FullName).GetTotalSizeAsync(items);
        }

        public Task MoveFileAsync(string source, string dest) =>
            ProviderFor(source).MoveFileAsync(source, dest);

        public Task MoveDirectoryAsync(string source, string dest) =>
            ProviderFor(source).MoveDirectoryAsync(source, dest);

        public Task RenameAsync(string oldPath, string newPath, bool isFolder) =>
            ProviderFor(oldPath).RenameAsync(oldPath, newPath, isFolder);

        public Task CreateDirectoryAsync(string path) =>
            ProviderFor(path).CreateDirectoryAsync(path);

        public Task DeleteFileAsync(string path) =>
            ProviderFor(path).DeleteFileAsync(path);

        public Task DeleteDirectoryAsync(string path, CancellationToken ct = default) =>
            ProviderFor(path).DeleteDirectoryAsync(path, ct);

        // Local<->FTP transfers are the only cross-provider case (FTP<->FTP can't occur — at
        // most one pane is ever FTP). Same-provider transfers fall through to that provider's
        // own implementation unchanged.
        public Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                        IProgress<int>? progress, long processedSize, long totalSize,
                                        CancellationToken ct)
        {
            bool sourceFtp = RemotePath.IsFtp(source);
            bool destFtp = RemotePath.IsFtp(dest);
            if (!sourceFtp && !destFtp)
            {
                return _local.CopyFileAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            if (!sourceFtp)
            {
                return Ftp.UploadFileAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            if (!destFtp)
            {
                return Ftp.DownloadFileAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            throw new NotSupportedException("FTP-to-FTP transfers are not supported.");
        }

        public Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool delete, bool overwrite,
                                             IProgress<int>? progress, long processedSize, long totalSize,
                                             CancellationToken ct)
        {
            bool sourceFtp = RemotePath.IsFtp(source);
            bool destFtp = RemotePath.IsFtp(dest);
            if (!sourceFtp && !destFtp)
            {
                return _local.CopyDirectoryAsync(source, dest, recursive, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            if (!sourceFtp)
            {
                return Ftp.UploadDirectoryAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            if (!destFtp)
            {
                return Ftp.DownloadDirectoryAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);
            }
            throw new NotSupportedException("FTP-to-FTP transfers are not supported.");
        }

        public Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                                string contentText, IProgress<string> results,
                                IProgress<string>? statusProgress, CancellationToken ct) =>
            ProviderFor(folder).SearchAsync(folder, pattern, topOnly, searchContent, contentText, results, statusProgress, ct);

        // items (the drag/paste source) and destPath (the other pane) can belong to different
        // providers — e.g. selecting local files and pasting into an FTP-browsed pane. When they
        // match, delegate straight to that provider (existing behavior); when they don't, walk
        // the source tree via the source provider and check existence via the destination
        // provider, matching each provider's own recursive-duplicate logic one level up.
        public async Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath)
        {
            if (items.Count == 0)
            {
                return new List<string>();
            }
            bool sourceFtp = RemotePath.IsFtp(items[0].FullName);
            bool destFtp = RemotePath.IsFtp(destPath);
            if (sourceFtp == destFtp)
            {
                return await (sourceFtp ? Ftp : _local).GetDuplicatesAsync(items, destPath);
            }

            var srcProvider = sourceFtp ? Ftp : (IFileSystemProvider)_local;
            var destProvider = destFtp ? Ftp : (IFileSystemProvider)_local;
            var duplicates = new List<string>();
            foreach (var (fullName, isFolder) in items)
            {
                var targetPath = RemotePath.CombineChild(destPath, Path.GetFileName(fullName));
                if (isFolder)
                {
                    await CollectCrossDuplicatesAsync(srcProvider, destProvider, fullName, targetPath, duplicates);
                }
                else if (await destProvider.FileExistsAsync(targetPath))
                {
                    duplicates.Add(fullName);
                }
            }
            return duplicates;
        }

        private static async Task CollectCrossDuplicatesAsync(IFileSystemProvider srcProvider, IFileSystemProvider destProvider,
            string sourceDir, string destDir, List<string> duplicates)
        {
            if (!await srcProvider.DirectoryExistsAsync(sourceDir))
            {
                return;
            }
            var filter = new DirectoryListingFilter();
            foreach (var file in await srcProvider.GetFilesAsync(sourceDir, filter, CancellationToken.None))
            {
                var targetPath = RemotePath.CombineChild(destDir, Path.GetFileName(file));
                if (await destProvider.FileExistsAsync(targetPath))
                {
                    duplicates.Add(file);
                }
            }
            foreach (var dir in await srcProvider.GetDirectoriesAsync(sourceDir, filter, CancellationToken.None))
            {
                var targetSubDir = RemotePath.CombineChild(destDir, Path.GetFileName(dir));
                await CollectCrossDuplicatesAsync(srcProvider, destProvider, dir, targetSubDir, duplicates);
            }
        }

        public Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            if (items.Count == 0)
            {
                return Task.FromResult(new List<string>());
            }
            // Items always come from a single pane's selection, so they share one provider.
            return ProviderFor(items[0].FullName).GetNonEmptyFoldersAsync(items);
        }
    }
}
