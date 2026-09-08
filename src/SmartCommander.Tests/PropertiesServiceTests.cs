using SmartCommander.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    // Integration tests against a temp directory (real file I/O), like ChecksumServiceTests.
    public class PropertiesServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly PropertiesService _svc = new();

        public PropertiesServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "SCPropsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                // Clear any read-only bits a test set, so the recursive delete succeeds.
                foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                }
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
        public async Task Get_File_ReturnsSizeAndTimestamps()
        {
            var path = WriteFile("a.bin", new byte[123]);

            var p = await _svc.GetAsync(path, isFolder: false);

            Assert.False(p.IsFolder);
            Assert.Equal(123, p.Size);
            Assert.Equal(File.GetCreationTime(path), p.CreationTime);
            Assert.Equal(File.GetLastWriteTime(path), p.LastWriteTime);
        }

        [Fact]
        public async Task Get_ReadOnlyFile_ReportsReadOnly()
        {
            var path = WriteFile("ro.bin", new byte[] { 1 });
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

            var p = await _svc.GetAsync(path, isFolder: false);

            Assert.True(p.IsReadOnly);
        }

        [Fact]
        public async Task ApplyAttributes_SetsAndClearsReadOnly()
        {
            var path = WriteFile("toggle.bin", new byte[] { 1 });

            await _svc.ApplyAttributesAsync(path, readOnly: true, hidden: false);
            Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));

            await _svc.ApplyAttributesAsync(path, readOnly: false, hidden: false);
            Assert.False(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));
        }

        [Fact]
        public async Task ApplyAttributes_HiddenIsWindowsOnly()
        {
            var path = WriteFile("hid.bin", new byte[] { 1 });

            await _svc.ApplyAttributesAsync(path, readOnly: false, hidden: true);

            var hidden = File.GetAttributes(path).HasFlag(FileAttributes.Hidden);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.True(hidden);
            }
            else
            {
                Assert.False(hidden);
            }
        }

        [Fact]
        public async Task ComputeFolderTally_CountsFilesFoldersAndBytes()
        {
            // root/f1.bin (10), root/sub/f2.bin (20), root/sub/nested/f3.bin (30)
            WriteFile("f1.bin", new byte[10]);
            Directory.CreateDirectory(Path.Combine(_root, "sub", "nested"));
            File.WriteAllBytes(Path.Combine(_root, "sub", "f2.bin"), new byte[20]);
            File.WriteAllBytes(Path.Combine(_root, "sub", "nested", "f3.bin"), new byte[30]);

            var tally = await _svc.ComputeFolderTallyAsync(_root, null, CancellationToken.None);

            Assert.Equal(3, tally.Files);
            Assert.Equal(2, tally.Folders);
            Assert.Equal(60, tally.Bytes);
        }

        [Fact]
        public async Task ComputeFolderTally_EmptyFolder_IsAllZero()
        {
            var tally = await _svc.ComputeFolderTallyAsync(_root, null, CancellationToken.None);

            Assert.Equal(0, tally.Files);
            Assert.Equal(0, tally.Folders);
            Assert.Equal(0, tally.Bytes);
        }

        [Fact]
        public async Task ComputeFolderTally_ReportsFinalProgress()
        {
            WriteFile("f1.bin", new byte[5]);

            FolderTally? last = null;
            var progress = new Progress<FolderTally>(t => last = t);

            var tally = await _svc.ComputeFolderTallyAsync(_root, progress, CancellationToken.None);

            // The Progress callback marshals asynchronously; give it a moment to land.
            for (var i = 0; i < 50 && last == null; i++)
            {
                await Task.Delay(10);
            }

            Assert.NotNull(last);
            Assert.Equal(tally, last);
        }

        [Fact]
        public async Task ComputeFolderTally_AlreadyCancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _svc.ComputeFolderTallyAsync(_root, null, cts.Token));
        }
    }
}
