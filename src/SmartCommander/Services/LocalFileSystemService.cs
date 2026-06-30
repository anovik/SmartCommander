using Serilog;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    public class LocalFileSystemService : IFileSystemService
    {
        public Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, EnumerationOptions options, CancellationToken ct)
        {
            return Task.Run<IReadOnlyList<string>>(
                () => Directory.EnumerateDirectories(path, "*", options).ToList(), ct);
        }

        public Task<IReadOnlyList<string>> GetFilesAsync(string path, EnumerationOptions options, CancellationToken ct)
        {
            return Task.Run<IReadOnlyList<string>>(
                () => Directory.EnumerateFiles(path, "*", options).ToList(), ct);
        }

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public string? GetDirectoryParent(string path) => Directory.GetParent(path)?.FullName;

        public string? GetPathRoot(string path) => Path.GetPathRoot(new FileInfo(path).FullName);

        public Task<long> GetFileSizeAsync(string path) =>
            Task.FromResult(new FileInfo(path).Length);

        public Task<DateTime> GetCreationTimeAsync(string path) =>
            Task.FromResult(File.GetCreationTime(path));

        public Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items) =>
            Task.Run(() => GetTotalSizeCore(items));

        public Task MoveFileAsync(string source, string dest) =>
            Task.Run(() => File.Move(source, dest));

        public Task MoveDirectoryAsync(string source, string dest) =>
            Task.Run(() => Directory.Move(source, dest));

        public Task CreateDirectoryAsync(string path) =>
            Task.Run(() => Directory.CreateDirectory(path));

        public Task DeleteFileAsync(string path) =>
            Task.Run(() => File.Delete(path));

        public Task DeleteDirectoryAsync(string path) =>
            Task.Run(() => DeleteDirectoryWithHiddenFiles(path));

        public Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                        IProgress<int>? progress, long processedSize, long totalSize,
                                        CancellationToken ct)
        {
            return Task.Run(() =>
            {
                long size = processedSize;
                CopyFileSync(source, dest, delete, overwrite, ct, progress, ref size, totalSize);
                return size;
            }, ct);
        }

        public Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool overwrite,
                                             IProgress<int>? progress, long processedSize, long totalSize,
                                             CancellationToken ct)
        {
            return Task.Run(() =>
            {
                long size = processedSize;
                CopyDirectorySync(source, dest, recursive, overwrite, ct, progress, ref size, totalSize);
                return size;
            }, ct);
        }

        public Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                                string contentText, IProgress<string> results,
                                IProgress<string>? statusProgress, CancellationToken ct)
        {
            return Task.Run(
                () => SearchCore(folder, pattern, topOnly, searchContent, contentText, results, statusProgress, ct),
                ct);
        }

        public Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath)
        {
            return Task.Run(() => GetDuplicatesCore(items, destPath));
        }

        public Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            return Task.Run(() => GetNonEmptyFoldersCore(items));
        }

        private static long GetTotalSizeCore(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            long totalSize = 0;
            foreach (var (fullName, isFolder) in items)
            {
                totalSize += isFolder
                    ? GetDirectorySize(new DirectoryInfo(fullName))
                    : new FileInfo(fullName).Length;
            }
            return totalSize;
        }

        private static long GetDirectorySize(DirectoryInfo d)
        {
            long size = d.GetFiles().Sum(f => f.Length);
            foreach (var sub in d.GetDirectories())
            {
                size += GetDirectorySize(sub);
            }
            return size;
        }

        private static void SearchCore(string folderPath, string pattern, bool topOnly, bool searchContent,
                                       string contentText, IProgress<string> results,
                                       IProgress<string>? statusProgress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            statusProgress?.Report(folderPath);
            try
            {
                if (searchContent)
                {
                    foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            if (File.ReadLines(file).Any(line => line.Contains(contentText)))
                            {
                                results.Report(file);
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    foreach (var dir in Directory.GetDirectories(folderPath, pattern, SearchOption.TopDirectoryOnly))
                    {
                        results.Report(dir);
                    }
                    foreach (var file in Directory.GetFiles(folderPath, pattern))
                    {
                        results.Report(file);
                    }
                }

                if (!topOnly)
                {
                    foreach (var subDir in Directory.GetDirectories(folderPath))
                    {
                        ct.ThrowIfCancellationRequested();
                        SearchCore(subDir, pattern, topOnly, searchContent, contentText, results, statusProgress, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Search failed in {Folder}", folderPath);
            }
        }

        private static List<string> GetDuplicatesCore(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath)
        {
            var duplicates = new List<string>();
            foreach (var (fullName, isFolder) in items)
            {
                string targetPath = Path.Combine(destPath, Path.GetFileName(fullName));
                if (isFolder)
                {
                    CollectDuplicatesInDirectory(fullName, targetPath, duplicates);
                }
                else if (File.Exists(targetPath))
                {
                    duplicates.Add(fullName);
                }
            }
            return duplicates;
        }

        private static void CollectDuplicatesInDirectory(string sourceDir, string destDir, List<string> duplicates)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                return;
            }
            foreach (var file in dir.GetFiles())
            {
                if (File.Exists(Path.Combine(destDir, file.Name)))
                {
                    duplicates.Add(file.FullName);
                }
            }
            foreach (var sub in dir.GetDirectories())
            {
                CollectDuplicatesInDirectory(sub.FullName, Path.Combine(destDir, sub.Name), duplicates);
            }
        }

        private static List<string> GetNonEmptyFoldersCore(IReadOnlyList<(string FullName, bool IsFolder)> items)
        {
            var result = new List<string>();
            if (!OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty)
            {
                return result;
            }
            foreach (var (fullName, isFolder) in items)
            {
                if (isFolder && Directory.EnumerateFileSystemEntries(fullName).Any())
                {
                    result.Add(fullName);
                }
            }
            return result;
        }

        private static void ReportProgress(IProgress<int>? progress, long processedSize, long totalSize)
        {
            if (progress == null)
            {
                return;
            }
            progress.Report(totalSize == 0 ? 0 : Convert.ToInt32(processedSize * 100 / totalSize));
        }

        private static void DeleteDirectoryWithHiddenFiles(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };
            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }
            directory.Delete(true);
        }

        private static void SetNormalFileAttributes(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            new FileInfo(path).Attributes = FileAttributes.Normal;
        }

        private static void CopyFileSync(string source, string dest, bool delete, bool overwrite,
                                         CancellationToken ct, IProgress<int>? progress,
                                         ref long processedSize, long totalSize)
        {
            long size = new FileInfo(source).Length;
            ct.ThrowIfCancellationRequested();

            if (!overwrite && File.Exists(dest))
            {
                processedSize += size;
                ReportProgress(progress, processedSize, totalSize);
                return;
            }

            if (delete && OperatingSystem.IsWindows() &&
                Path.GetPathRoot(source) == Path.GetPathRoot(dest))
            {
                File.Move(source, dest, overwrite);
                processedSize += size;
                ReportProgress(progress, processedSize, totalSize);
                return;
            }

            const int bufferSize = 1024 * 1024;
            const long limit = 10 * bufferSize;

            if (size > limit)
            {
                using var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Write);
                using var to = new FileStream(dest, FileMode.Create);
                int readCount;
                byte[] buffer = new byte[bufferSize];
                while ((readCount = from.Read(buffer, 0, bufferSize)) != 0)
                {
                    ct.ThrowIfCancellationRequested();
                    to.Write(buffer, 0, readCount);
                    processedSize += readCount;
                    ReportProgress(progress, processedSize, totalSize);
                }
            }
            else
            {
                File.Copy(source, dest, overwrite);
                processedSize += size;
                ReportProgress(progress, processedSize, totalSize);
            }

            if (delete)
            {
                File.Delete(source);
            }
        }

        private static void CopyDirectorySync(string sourceDir, string destinationDir, bool recursive,
                                              bool overwrite, CancellationToken ct, IProgress<int>? progress,
                                              ref long processedSize, long totalSize)
        {
            ct.ThrowIfCancellationRequested();
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                if (File.Exists(targetFilePath))
                {
                    SetNormalFileAttributes(targetFilePath);
                }
                CopyFileSync(file.FullName, targetFilePath, delete: false, overwrite, ct,
                    progress, ref processedSize, totalSize);
            }
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    ct.ThrowIfCancellationRequested();
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectorySync(subDir.FullName, newDestinationDir, true, overwrite, ct,
                        progress, ref processedSize, totalSize);
                }
            }
        }
    }
}
