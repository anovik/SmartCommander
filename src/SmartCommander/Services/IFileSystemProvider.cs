using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // Per-backend filesystem contract. Nothing here may take or expose a type that only
    // makes sense for local disks, and existence/path checks are async because a remote
    // backend answers them over a socket.
    public interface IFileSystemProvider
    {
        // Listing and checks
        Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, DirectoryListingFilter filter, CancellationToken ct);
        Task<IReadOnlyList<string>> GetFilesAsync(string path, DirectoryListingFilter filter, CancellationToken ct);
        Task<bool> DirectoryExistsAsync(string path, CancellationToken ct = default);
        Task<bool> FileExistsAsync(string path, CancellationToken ct = default);
        Task<string?> GetDirectoryParentAsync(string path, CancellationToken ct = default);
        Task<string?> GetPathRootAsync(string path, CancellationToken ct = default);

        // Metadata — intended to be called from a background thread; no Task.Run internally
        Task<long> GetFileSizeAsync(string path);
        Task<DateTime> GetCreationTimeAsync(string path);
        Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items);

        // Mutating
        Task MoveFileAsync(string source, string dest);
        Task MoveDirectoryAsync(string source, string dest);
        Task RenameAsync(string oldPath, string newPath, bool isFolder);
        Task CreateDirectoryAsync(string path);
        Task DeleteFileAsync(string path);
        Task DeleteDirectoryAsync(string path, CancellationToken ct = default);

        // Copy/move — returns updated processedSize for progress tracking across multiple items
        Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                 IProgress<int>? progress, long processedSize, long totalSize,
                                 CancellationToken ct);
        Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool delete, bool overwrite,
                                      IProgress<int>? progress, long processedSize, long totalSize,
                                      CancellationToken ct);

        // Search — results and current-folder status each streamed via separate IProgress<string>
        Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                         string contentText, IProgress<string> results,
                         IProgress<string>? statusProgress, CancellationToken ct);

        // Bulk helpers
        Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath);
        Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items);
    }
}
