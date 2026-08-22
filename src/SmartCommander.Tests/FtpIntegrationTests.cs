using SmartCommander.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    // Opt-in only: hits real public FTP servers, so these stay out of the default `dotnet test`
    // run. Each test self-gates on an env var and no-ops (reports Passed) when it's unset. Run with:
    //   SMARTCOMMANDER_FTP_INTEGRATION_TESTS=1 dotnet test src/SmartCommander.Tests
    public class FtpIntegrationTests
    {
        private static bool OptedIn =>
            Environment.GetEnvironmentVariable("SMARTCOMMANDER_FTP_INTEGRATION_TESTS") == "1";

        // Read-only public demo server (Rebex): host test.rebex.net, demo/password, /pub/example.
        private static async Task<FtpFileSystemProvider> ConnectRebexAsync()
        {
            var provider = new FtpFileSystemProvider();
            await provider.ConnectAsync("test.rebex.net", 21, "demo", "password", anonymous: false, CancellationToken.None);
            return provider;
        }

        // Read-write public test server (dlptest.com): host ftp.dlptest.com, passive-only,
        // uploaded files auto-delete after ~10 minutes so tests don't need to worry about buildup.
        private static async Task<FtpFileSystemProvider> ConnectDlpTestAsync()
        {
            var provider = new FtpFileSystemProvider();
            await provider.ConnectAsync("ftp.dlptest.com", 21, "dlpuser", "rNrKYTX9g7z3RgJRmxWuGHbeu",
                anonymous: false, CancellationToken.None);
            return provider;
        }

        [Fact]
        public async Task Rebex_ListRootDirectory_ContainsPubFolder()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectRebexAsync();

            var dirs = await provider.GetDirectoriesAsync(RemotePath.Combine("/"), new DirectoryListingFilter(), CancellationToken.None);

            Assert.Contains(dirs, d => d.EndsWith("/pub"));
        }

        [Fact]
        public async Task Rebex_ListPubExample_ContainsReadme()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectRebexAsync();

            var files = await provider.GetFilesAsync(RemotePath.Combine("/pub/example"), new DirectoryListingFilter(), CancellationToken.None);

            Assert.Contains(files, f => f.EndsWith("readme.txt"));
        }

        [Fact]
        public async Task Rebex_DownloadReadme_ProducesNonEmptyLocalFile()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectRebexAsync();
            var localPath = Path.Combine(Path.GetTempPath(), "SCTests_rebex_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                await provider.DownloadFileAsync(RemotePath.Combine("/pub/example/readme.txt"), localPath,
                    delete: false, overwrite: true, progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

                Assert.True(File.Exists(localPath));
                Assert.True(new FileInfo(localPath).Length > 0);
            }
            finally
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
        }

        [Fact]
        public async Task DlpTest_UploadDownloadDelete_RoundTrips()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectDlpTestAsync();
            var name = "SCTests_" + Guid.NewGuid().ToString("N") + ".txt";
            var localSource = Path.Combine(Path.GetTempPath(), name);
            var localDest = Path.Combine(Path.GetTempPath(), "down_" + name);
            var remotePath = RemotePath.CombineChild(RemotePath.Combine("/"), name);
            File.WriteAllText(localSource, "SmartCommander FTP integration test");

            try
            {
                await provider.UploadFileAsync(localSource, remotePath, delete: false, overwrite: true,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);
                Assert.True(await provider.FileExistsAsync(remotePath));

                await provider.DownloadFileAsync(remotePath, localDest, delete: false, overwrite: true,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);
                Assert.Equal("SmartCommander FTP integration test", File.ReadAllText(localDest));

                await provider.DeleteFileAsync(remotePath);
                Assert.False(await provider.FileExistsAsync(remotePath));
            }
            finally
            {
                if (File.Exists(localSource))
                {
                    File.Delete(localSource);
                }
                if (File.Exists(localDest))
                {
                    File.Delete(localDest);
                }
            }
        }

        [Fact]
        public async Task DlpTest_RenameFile_UpdatesRemoteName()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectDlpTestAsync();
            var oldName = "SCTests_" + Guid.NewGuid().ToString("N") + "_old.txt";
            var newName = "SCTests_" + Guid.NewGuid().ToString("N") + "_new.txt";
            var localSource = Path.Combine(Path.GetTempPath(), oldName);
            var oldRemote = RemotePath.CombineChild(RemotePath.Combine("/"), oldName);
            var newRemote = RemotePath.CombineChild(RemotePath.Combine("/"), newName);
            File.WriteAllText(localSource, "rename test");

            try
            {
                await provider.UploadFileAsync(localSource, oldRemote, delete: false, overwrite: true,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

                await provider.RenameAsync(oldRemote, newRemote, isFolder: false);

                Assert.False(await provider.FileExistsAsync(oldRemote));
                Assert.True(await provider.FileExistsAsync(newRemote));

                await provider.DeleteFileAsync(newRemote);
            }
            finally
            {
                if (File.Exists(localSource))
                {
                    File.Delete(localSource);
                }
            }
        }

        [Fact]
        public async Task DlpTest_CreateAndDeleteDirectory_RoundTrips()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectDlpTestAsync();
            var dirName = "SCTests_" + Guid.NewGuid().ToString("N");
            var remoteDir = RemotePath.CombineChild(RemotePath.Combine("/"), dirName);

            await provider.CreateDirectoryAsync(remoteDir);
            Assert.True(await provider.DirectoryExistsAsync(remoteDir));

            await provider.DeleteDirectoryAsync(remoteDir);
            Assert.False(await provider.DirectoryExistsAsync(remoteDir));
        }

        // Read-write public test server (dlp-test.com - note the hyphen, a distinct host from
        // dlptest.com above): host ftp.dlp-test.com, true anonymous login (no credentials needed).
        private static async Task<FtpFileSystemProvider> ConnectDlpTestPubAsync()
        {
            var provider = new FtpFileSystemProvider();
            await provider.ConnectAsync("ftp.dlp-test.com", 21, "anonymous", "", anonymous: true, CancellationToken.None);
            return provider;
        }

        [Fact]
        public async Task DlpTestPub_AnonymousListUploadDelete_RoundTrips()
        {
            if (!OptedIn)
            {
                return;
            }

            await using var provider = await ConnectDlpTestPubAsync();
            var name = "SCTests_" + Guid.NewGuid().ToString("N") + ".txt";
            var localSource = Path.Combine(Path.GetTempPath(), name);
            var remotePath = RemotePath.CombineChild(RemotePath.Combine("/"), name);
            File.WriteAllText(localSource, "SmartCommander FTP anonymous integration test");

            try
            {
                // Listing must succeed anonymously before we ever touch upload/delete.
                var dirsBeforeUpload = await provider.GetDirectoriesAsync(RemotePath.Combine("/"), new DirectoryListingFilter(), CancellationToken.None);
                var filesBeforeUpload = await provider.GetFilesAsync(RemotePath.Combine("/"), new DirectoryListingFilter(), CancellationToken.None);
                Assert.NotNull(dirsBeforeUpload);
                Assert.DoesNotContain(filesBeforeUpload, f => f.EndsWith(name));

                await provider.UploadFileAsync(localSource, remotePath, delete: false, overwrite: true,
                    progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);
                Assert.True(await provider.FileExistsAsync(remotePath));

                await provider.DeleteFileAsync(remotePath);
                Assert.False(await provider.FileExistsAsync(remotePath));
            }
            finally
            {
                if (File.Exists(localSource))
                {
                    File.Delete(localSource);
                }
            }
        }

        [Fact]
        public async Task DlpTest_FailedConnect_ThrowsWithoutSilentDisconnect()
        {
            if (!OptedIn)
            {
                return;
            }

            var provider = new FtpFileSystemProvider();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                provider.ConnectAsync("ftp.dlptest.com", 21, "not-a-real-user", "not-a-real-password",
                    anonymous: false, CancellationToken.None));
        }
    }
}
