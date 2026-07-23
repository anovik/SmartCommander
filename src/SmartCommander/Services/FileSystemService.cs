using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // Composite the ViewModels talk to: dispatches each call to the provider that
    // owns the path. Only the local provider exists so far.
    public class FileSystemService : IFileSystemService
    {
        private readonly IFileSystemProvider _local;

        public FileSystemService(IFileSystemProvider local)
        {
            _local = local;
        }

        public Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, DirectoryListingFilter filter, CancellationToken ct) =>
            _local.GetDirectoriesAsync(path, filter, ct);

        public Task<IReadOnlyList<string>> GetFilesAsync(string path, DirectoryListingFilter filter, CancellationToken ct) =>
            _local.GetFilesAsync(path, filter, ct);

        public Task<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
            _local.DirectoryExistsAsync(path, ct);

        public Task<string?> GetDirectoryParentAsync(string path, CancellationToken ct = default) =>
            _local.GetDirectoryParentAsync(path, ct);

        public Task<string?> GetPathRootAsync(string path, CancellationToken ct = default) =>
            _local.GetPathRootAsync(path, ct);

        public Task<long> GetFileSizeAsync(string path) =>
            _local.GetFileSizeAsync(path);

        public Task<DateTime> GetCreationTimeAsync(string path) =>
            _local.GetCreationTimeAsync(path);

        public Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items) =>
            _local.GetTotalSizeAsync(items);

        public Task MoveFileAsync(string source, string dest) =>
            _local.MoveFileAsync(source, dest);

        public Task MoveDirectoryAsync(string source, string dest) =>
            _local.MoveDirectoryAsync(source, dest);

        public Task RenameAsync(string oldPath, string newPath, bool isFolder) =>
            _local.RenameAsync(oldPath, newPath, isFolder);

        public Task CreateDirectoryAsync(string path) =>
            _local.CreateDirectoryAsync(path);

        public Task DeleteFileAsync(string path) =>
            _local.DeleteFileAsync(path);

        public Task DeleteDirectoryAsync(string path, CancellationToken ct = default) =>
            _local.DeleteDirectoryAsync(path, ct);

        public Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                        IProgress<int>? progress, long processedSize, long totalSize,
                                        CancellationToken ct) =>
            _local.CopyFileAsync(source, dest, delete, overwrite, progress, processedSize, totalSize, ct);

        public Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool delete, bool overwrite,
                                             IProgress<int>? progress, long processedSize, long totalSize,
                                             CancellationToken ct) =>
            _local.CopyDirectoryAsync(source, dest, recursive, delete, overwrite, progress, processedSize, totalSize, ct);

        public Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                                string contentText, IProgress<string> results,
                                IProgress<string>? statusProgress, CancellationToken ct) =>
            _local.SearchAsync(folder, pattern, topOnly, searchContent, contentText, results, statusProgress, ct);

        public Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath) =>
            _local.GetDuplicatesAsync(items, destPath);

        public Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items) =>
            _local.GetNonEmptyFoldersAsync(items);
    }
}
