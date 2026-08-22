using SmartCommander.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    // Composite scheme-parsing and pairwise-dispatch tests: a hand-written local fake plus the
    // ftp:// "no connection active" guard. Dispatch against a real FTP connection is covered
    // separately by the opt-in integration tests in FtpIntegrationTests.cs.
    public class FileSystemServiceTests
    {
        private const string LocalPath = @"C:\local\file.txt";
        private const string FtpPath = "ftp:///remote/file.txt";

        private sealed class FakeFileSystemProvider : IFileSystemProvider
        {
            public List<string> Calls { get; } = new();

            public Task<IReadOnlyList<string>> GetDirectoriesAsync(string path, DirectoryListingFilter filter, CancellationToken ct)
            {
                Calls.Add(nameof(GetDirectoriesAsync));
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }

            public Task<IReadOnlyList<string>> GetFilesAsync(string path, DirectoryListingFilter filter, CancellationToken ct)
            {
                Calls.Add(nameof(GetFilesAsync));
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }

            public Task<bool> DirectoryExistsAsync(string path, CancellationToken ct = default)
            {
                Calls.Add(nameof(DirectoryExistsAsync));
                return Task.FromResult(true);
            }

            public Task<bool> FileExistsAsync(string path, CancellationToken ct = default)
            {
                Calls.Add(nameof(FileExistsAsync));
                return Task.FromResult(false);
            }

            public Task<string?> GetDirectoryParentAsync(string path, CancellationToken ct = default)
            {
                Calls.Add(nameof(GetDirectoryParentAsync));
                return Task.FromResult<string?>(null);
            }

            public Task<string?> GetPathRootAsync(string path, CancellationToken ct = default)
            {
                Calls.Add(nameof(GetPathRootAsync));
                return Task.FromResult<string?>(@"C:\");
            }

            public Task<long> GetFileSizeAsync(string path)
            {
                Calls.Add(nameof(GetFileSizeAsync));
                return Task.FromResult(0L);
            }

            public Task<DateTime> GetCreationTimeAsync(string path)
            {
                Calls.Add(nameof(GetCreationTimeAsync));
                return Task.FromResult(DateTime.MinValue);
            }

            public Task<long> GetTotalSizeAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
            {
                Calls.Add(nameof(GetTotalSizeAsync));
                return Task.FromResult(0L);
            }

            public Task MoveFileAsync(string source, string dest)
            {
                Calls.Add(nameof(MoveFileAsync));
                return Task.CompletedTask;
            }

            public Task MoveDirectoryAsync(string source, string dest)
            {
                Calls.Add(nameof(MoveDirectoryAsync));
                return Task.CompletedTask;
            }

            public Task RenameAsync(string oldPath, string newPath, bool isFolder)
            {
                Calls.Add(nameof(RenameAsync));
                return Task.CompletedTask;
            }

            public Task CreateDirectoryAsync(string path)
            {
                Calls.Add(nameof(CreateDirectoryAsync));
                return Task.CompletedTask;
            }

            public Task DeleteFileAsync(string path)
            {
                Calls.Add(nameof(DeleteFileAsync));
                return Task.CompletedTask;
            }

            public Task DeleteDirectoryAsync(string path, CancellationToken ct = default)
            {
                Calls.Add(nameof(DeleteDirectoryAsync));
                return Task.CompletedTask;
            }

            public Task<long> CopyFileAsync(string source, string dest, bool delete, bool overwrite,
                                            IProgress<int>? progress, long processedSize, long totalSize,
                                            CancellationToken ct)
            {
                Calls.Add(nameof(CopyFileAsync));
                return Task.FromResult(processedSize);
            }

            public Task<long> CopyDirectoryAsync(string source, string dest, bool recursive, bool delete, bool overwrite,
                                                 IProgress<int>? progress, long processedSize, long totalSize,
                                                 CancellationToken ct)
            {
                Calls.Add(nameof(CopyDirectoryAsync));
                return Task.FromResult(processedSize);
            }

            public Task SearchAsync(string folder, string pattern, bool topOnly, bool searchContent,
                                    string contentText, IProgress<string> results,
                                    IProgress<string>? statusProgress, CancellationToken ct)
            {
                Calls.Add(nameof(SearchAsync));
                return Task.CompletedTask;
            }

            public Task<List<string>> GetDuplicatesAsync(IReadOnlyList<(string FullName, bool IsFolder)> items, string destPath)
            {
                Calls.Add(nameof(GetDuplicatesAsync));
                return Task.FromResult(new List<string>());
            }

            public Task<List<string>> GetNonEmptyFoldersAsync(IReadOnlyList<(string FullName, bool IsFolder)> items)
            {
                Calls.Add(nameof(GetNonEmptyFoldersAsync));
                return Task.FromResult(new List<string>());
            }
        }

        [Fact]
        public void IsFtpConnected_NoConnection_IsFalse()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());
            Assert.False(fs.IsFtpConnected);
            Assert.Null(fs.FtpHost);
        }

        [Fact]
        public async Task DisconnectFtpAsync_NoConnection_NoOps()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());
            await fs.DisconnectFtpAsync();
            Assert.False(fs.IsFtpConnected);
        }

        [Fact]
        public async Task GetDirectoriesAsync_LocalPath_DispatchesToLocalProvider()
        {
            var local = new FakeFileSystemProvider();
            var fs = new FileSystemService(local);

            await fs.GetDirectoriesAsync(LocalPath, new DirectoryListingFilter(), CancellationToken.None);

            Assert.Contains(nameof(FakeFileSystemProvider.GetDirectoriesAsync), local.Calls);
        }

        [Fact]
        public async Task GetDirectoriesAsync_FtpPath_NoConnection_Throws()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fs.GetDirectoriesAsync(FtpPath, new DirectoryListingFilter(), CancellationToken.None));
        }

        [Fact]
        public async Task DirectoryExistsAsync_FtpPath_NoConnection_Throws()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() => fs.DirectoryExistsAsync(FtpPath));
        }

        [Fact]
        public async Task CopyFileAsync_LocalToLocal_DispatchesToLocalProvider()
        {
            var local = new FakeFileSystemProvider();
            var fs = new FileSystemService(local);

            await fs.CopyFileAsync(LocalPath, @"C:\local\dest.txt", delete: false, overwrite: false,
                progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

            Assert.Contains(nameof(FakeFileSystemProvider.CopyFileAsync), local.Calls);
        }

        [Fact]
        public async Task CopyFileAsync_LocalToFtp_NoConnection_Throws()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fs.CopyFileAsync(LocalPath, FtpPath, delete: false, overwrite: false,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None));
        }

        [Fact]
        public async Task CopyFileAsync_FtpToLocal_NoConnection_Throws()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fs.CopyFileAsync(FtpPath, LocalPath, delete: false, overwrite: false,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None));
        }

        [Fact]
        public async Task GetTotalSizeAsync_EmptyItems_ReturnsZeroWithoutTouchingAnyProvider()
        {
            var local = new FakeFileSystemProvider();
            var fs = new FileSystemService(local);

            var size = await fs.GetTotalSizeAsync(new List<(string FullName, bool IsFolder)>());

            Assert.Equal(0, size);
            Assert.Empty(local.Calls);
        }

        [Fact]
        public async Task GetDuplicatesAsync_EmptyItems_ReturnsEmptyWithoutTouchingAnyProvider()
        {
            var local = new FakeFileSystemProvider();
            var fs = new FileSystemService(local);

            var duplicates = await fs.GetDuplicatesAsync(new List<(string FullName, bool IsFolder)>(), LocalPath);

            Assert.Empty(duplicates);
            Assert.Empty(local.Calls);
        }

        [Fact]
        public async Task GetDuplicatesAsync_LocalToLocal_DispatchesToLocalProvider()
        {
            var local = new FakeFileSystemProvider();
            var fs = new FileSystemService(local);

            await fs.GetDuplicatesAsync(new List<(string FullName, bool IsFolder)> { (LocalPath, false) }, @"C:\local");

            Assert.Contains(nameof(FakeFileSystemProvider.GetDuplicatesAsync), local.Calls);
        }

        [Fact]
        public async Task GetDuplicatesAsync_LocalToFtp_NoConnection_Throws()
        {
            var fs = new FileSystemService(new FakeFileSystemProvider());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fs.GetDuplicatesAsync(new List<(string FullName, bool IsFolder)> { (LocalPath, false) }, FtpPath));
        }
    }
}
