using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    public interface IFileSystemService
    {
        // Listing and checks
        Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, EnumerationOptions options, CancellationToken ct);
        Task<IReadOnlyList<string>> GetFilesAsync(string path, EnumerationOptions options, CancellationToken ct);
        bool DirectoryExists(string path);
        string? GetDirectoryParent(string path);
        string? GetPathRoot(string path);

        // Metadata — intended to be called from a background thread; no Task.Run internally
        Task<long> GetFileSizeAsync(string path);
        Task<DateTime> GetCreationTimeAsync(string path);
        Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items);

        // Mutating
        Task MoveFileAsync(string source, string dest);
        Task MoveDirectoryAsync(string source, string dest);
        Task CreateDirectoryAsync(string path);
        Task DeleteFileAsync(string path);
        Task DeleteDirectoryAsync(string path);

        // Copy/move — returns updated processedSize for progress tracking across multiple items
        Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                 IProgress<int>? progress, long processedSize, long totalSize,
                                 CancellationToken ct);
        Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool overwrite,
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
