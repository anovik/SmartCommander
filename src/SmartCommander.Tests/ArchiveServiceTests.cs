using ICSharpCode.SharpZipLib.Zip;
using SmartCommander.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    public class ArchiveServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly ArchiveService _svc = new();
        private static readonly IProgress<int> NoProgress = new Progress<int>();

        public ArchiveServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "SCArchiveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        // Builds root/src/{a.txt, sub/b.txt} with compressible content, returns the src dir path.
        private string BuildSampleTree()
        {
            var src = Path.Combine(_root, "src");
            Directory.CreateDirectory(Path.Combine(src, "sub"));
            File.WriteAllText(Path.Combine(src, "a.txt"), Repeat("alpha ", 2000));
            File.WriteAllText(Path.Combine(src, "sub", "b.txt"), Repeat("bravo ", 2000));
            return src;
        }

        private static string Repeat(string s, int count) => string.Concat(Enumerable.Repeat(s, count));

        private static long TotalSize(string dir) =>
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);

        private List<(string FullName, bool IsFolder)> FolderItem(string dir) => new() { (dir, true) };

        [Fact]
        public async Task PlainZip_RoundTrips_FileSetAndContentsAndStructure()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "plain.zip");
            var dest = Path.Combine(_root, "out");

            await _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Normal, null,
                TotalSize(src), NoProgress, CancellationToken.None);
            await _svc.ExtractZipAsync(zipPath, dest, null, NoProgress, CancellationToken.None);

            Assert.False(_svc.IsEncrypted(zipPath));
            Assert.True(File.Exists(Path.Combine(dest, "src", "a.txt")));
            Assert.True(File.Exists(Path.Combine(dest, "src", "sub", "b.txt")));
            Assert.Equal(File.ReadAllText(Path.Combine(src, "a.txt")),
                File.ReadAllText(Path.Combine(dest, "src", "a.txt")));
            Assert.Equal(File.ReadAllText(Path.Combine(src, "sub", "b.txt")),
                File.ReadAllText(Path.Combine(dest, "src", "sub", "b.txt")));
        }

        [Fact]
        public async Task EncryptedZip_RoundTrips_AndPasswordChecks()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "enc.zip");
            var dest = Path.Combine(_root, "out");
            const string password = "s3cr3t-pass";

            await _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Maximum, password,
                TotalSize(src), NoProgress, CancellationToken.None);

            Assert.True(_svc.IsEncrypted(zipPath));
            Assert.True(_svc.VerifyPassword(zipPath, password));
            Assert.False(_svc.VerifyPassword(zipPath, "wrong-pass"));

            await _svc.ExtractZipAsync(zipPath, dest, password, NoProgress, CancellationToken.None);
            Assert.Equal(File.ReadAllText(Path.Combine(src, "a.txt")),
                File.ReadAllText(Path.Combine(dest, "src", "a.txt")));
            Assert.Equal(File.ReadAllText(Path.Combine(src, "sub", "b.txt")),
                File.ReadAllText(Path.Combine(dest, "src", "sub", "b.txt")));
        }

        [Fact]
        public async Task ExtractZipAsync_WrongPassword_ThrowsInvalidPasswordException()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "enc.zip");
            var dest = Path.Combine(_root, "out");

            await _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Normal, "right-pass",
                TotalSize(src), NoProgress, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidPasswordException>(() =>
                _svc.ExtractZipAsync(zipPath, dest, "bad-pass", NoProgress, CancellationToken.None));
        }

        [Theory]
        [InlineData(ZipCompressionLevel.Store)]
        [InlineData(ZipCompressionLevel.Fastest)]
        [InlineData(ZipCompressionLevel.Normal)]
        [InlineData(ZipCompressionLevel.Maximum)]
        public async Task EachLevel_ProducesValidExtractableArchive(ZipCompressionLevel level)
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, $"lvl-{level}.zip");
            var dest = Path.Combine(_root, $"out-{level}");

            await _svc.CreateZipAsync(FolderItem(src), zipPath, level, null,
                TotalSize(src), NoProgress, CancellationToken.None);
            await _svc.ExtractZipAsync(zipPath, dest, null, NoProgress, CancellationToken.None);

            Assert.Equal(File.ReadAllText(Path.Combine(src, "a.txt")),
                File.ReadAllText(Path.Combine(dest, "src", "a.txt")));
        }

        [Fact]
        public async Task StoreOutput_IsNotSmallerThan_MaximumOutput_ForCompressibleInput()
        {
            var src = BuildSampleTree();
            var storeZip = Path.Combine(_root, "store.zip");
            var maxZip = Path.Combine(_root, "max.zip");

            await _svc.CreateZipAsync(FolderItem(src), storeZip, ZipCompressionLevel.Store, null,
                TotalSize(src), NoProgress, CancellationToken.None);
            await _svc.CreateZipAsync(FolderItem(src), maxZip, ZipCompressionLevel.Maximum, null,
                TotalSize(src), NoProgress, CancellationToken.None);

            Assert.True(new FileInfo(storeZip).Length >= new FileInfo(maxZip).Length);
        }

        [Fact]
        public async Task ExtractZipAsync_ZipSlipEntry_IsRejected()
        {
            var zipPath = Path.Combine(_root, "evil.zip");
            using (var stream = File.Create(zipPath))
            using (var zos = new ZipOutputStream(stream))
            {
                zos.SetLevel(0);
                var bytes = System.Text.Encoding.UTF8.GetBytes("pwned");
                // Raw name, bypassing ZipEntry.CleanName, to simulate a crafted archive.
                var entry = new ZipEntry("../evil.txt") { Size = bytes.Length };
                zos.PutNextEntry(entry);
                zos.Write(bytes, 0, bytes.Length);
                zos.CloseEntry();
                zos.Finish();
            }

            var dest = Path.Combine(_root, "out");
            await Assert.ThrowsAsync<IOException>(() =>
                _svc.ExtractZipAsync(zipPath, dest, null, NoProgress, CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        }

        [Fact]
        public async Task CreateZipAsync_AlreadyCancelledToken_ThrowsAndLeavesNoArchive()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "cancelled.zip");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Normal, null,
                    TotalSize(src), NoProgress, cts.Token));

            Assert.False(File.Exists(zipPath));
        }

        [Fact]
        public async Task CreateZipAsync_TargetAlreadyExists_ThrowsAndLeavesItUntouched()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "taken.zip");
            File.WriteAllText(zipPath, "not a zip");

            await Assert.ThrowsAsync<IOException>(() =>
                _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Normal, null,
                    TotalSize(src), NoProgress, CancellationToken.None));

            Assert.Equal("not a zip", File.ReadAllText(zipPath));
        }

        [Fact]
        public async Task ExtractZipAsync_AlreadyCancelledToken_Throws()
        {
            var src = BuildSampleTree();
            var zipPath = Path.Combine(_root, "src.zip");
            await _svc.CreateZipAsync(FolderItem(src), zipPath, ZipCompressionLevel.Normal, null,
                TotalSize(src), NoProgress, CancellationToken.None);

            var dest = Path.Combine(_root, "out");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _svc.ExtractZipAsync(zipPath, dest, null, NoProgress, cts.Token));
        }
    }
}
