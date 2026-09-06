using SmartCommander.Services;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    // Integration tests against a temp directory (real file I/O), like ArchiveServiceTests.
    public class ChecksumServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly ChecksumService _svc = new();
        private static readonly IProgress<int> NoProgress = new Progress<int>();

        // Known NIST/RFC vectors.
        private const string EmptyMd5 = "d41d8cd98f00b204e9800998ecf8427e";
        private const string EmptySha1 = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string AbcMd5 = "900150983cd24fb0d6963f7d28e17f72";
        private const string AbcSha1 = "a9993e364706816aba3e25717850c26c9cd0d89d";
        private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        public ChecksumServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "SCChecksumTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private string WriteFile(string name, byte[] content)
        {
            var path = Path.Combine(_root, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        [Fact]
        public async Task Compute_EmptyFile_MatchesKnownVectors()
        {
            var path = WriteFile("empty.bin", Array.Empty<byte>());

            var result = await _svc.ComputeAsync(path, NoProgress, CancellationToken.None);

            Assert.Equal(EmptyMd5, result.Md5);
            Assert.Equal(EmptySha1, result.Sha1);
            Assert.Equal(EmptySha256, result.Sha256);
        }

        [Fact]
        public async Task Compute_AbcFile_MatchesKnownVectors()
        {
            var path = WriteFile("abc.txt", Encoding.ASCII.GetBytes("abc"));

            var result = await _svc.ComputeAsync(path, NoProgress, CancellationToken.None);

            Assert.Equal(AbcMd5, result.Md5);
            Assert.Equal(AbcSha1, result.Sha1);
            Assert.Equal(AbcSha256, result.Sha256);
        }

        [Fact]
        public async Task Compute_LargeFile_ReportsProgressAndFinishesAtHundred()
        {
            // A few MB so the streaming loop runs over many buffers.
            var bytes = new byte[5 * 1024 * 1024];
            new Random(1).NextBytes(bytes);
            var path = WriteFile("large.bin", bytes);

            var seen100 = new TaskCompletionSource();
            var progress = new Progress<int>(p => { if (p == 100) { seen100.TrySetResult(); } });

            var result = await _svc.ComputeAsync(path, progress, CancellationToken.None);
            await seen100.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(32 * 2, result.Sha256.Length);
            Assert.Equal(16 * 2, result.Md5.Length);
            Assert.Equal(20 * 2, result.Sha1.Length);
        }

        [Fact]
        public async Task Compute_AlreadyCancelledToken_Throws()
        {
            var path = WriteFile("x.bin", new byte[] { 1, 2, 3 });
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _svc.ComputeAsync(path, NoProgress, cts.Token));
        }

        [Fact]
        public async Task TryReadSidecar_NoSidecar_ReturnsNull()
        {
            var path = WriteFile("plain.bin", new byte[] { 9 });

            Assert.Null(await _svc.TryReadSidecarAsync(path));
        }

        [Fact]
        public async Task TryReadSidecar_Sha256Sidecar_ReturnsHashToken()
        {
            var path = WriteFile("dl.iso", new byte[] { 1 });
            File.WriteAllText(path + ".sha256", AbcSha256 + " *dl.iso" + Environment.NewLine);

            Assert.Equal(AbcSha256, await _svc.TryReadSidecarAsync(path));
        }

        [Fact]
        public async Task TryReadSidecar_Md5Sidecar_ReturnsHashToken()
        {
            var path = WriteFile("dl2.iso", new byte[] { 1 });
            File.WriteAllText(path + ".md5", AbcMd5 + "  dl2.iso\n");

            Assert.Equal(AbcMd5, await _svc.TryReadSidecarAsync(path));
        }

        [Fact]
        public async Task TryReadSidecar_PrefersSha256OverMd5()
        {
            var path = WriteFile("dl3.iso", new byte[] { 1 });
            File.WriteAllText(path + ".md5", AbcMd5);
            File.WriteAllText(path + ".sha256", AbcSha256);

            Assert.Equal(AbcSha256, await _svc.TryReadSidecarAsync(path));
        }
    }
}
