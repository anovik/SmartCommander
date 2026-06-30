using SmartCommander.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartCommander.Tests
{
    public class LocalFileSystemServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly LocalFileSystemService _fs = new();

        public LocalFileSystemServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "SCTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private string TempFile(string name, string content = "hello")
        {
            var path = Path.Combine(_root, name);
            File.WriteAllText(path, content);
            return path;
        }

        private string TempDir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact]
        public async Task GetDirectoriesAsync_ReturnsSubdirectories()
        {
            TempDir("sub1");
            TempDir("sub2");
            TempFile("file.txt");

            var dirs = await _fs.GetDirectoriesAsync(_root, new EnumerationOptions(), CancellationToken.None);

            Assert.Equal(2, dirs.Count);
            Assert.Contains(dirs, d => d.EndsWith("sub1"));
            Assert.Contains(dirs, d => d.EndsWith("sub2"));
        }

        [Fact]
        public async Task GetFilesAsync_ReturnsFiles()
        {
            TempFile("a.txt");
            TempFile("b.txt");
            TempDir("adir");

            var files = await _fs.GetFilesAsync(_root, new EnumerationOptions(), CancellationToken.None);

            Assert.Equal(2, files.Count);
            Assert.Contains(files, f => f.EndsWith("a.txt"));
            Assert.Contains(files, f => f.EndsWith("b.txt"));
        }

        [Fact]
        public async Task GetTotalSizeAsync_SingleFile_ReturnsFileLength()
        {
            var path = TempFile("data.bin", "12345");
            long expected = new FileInfo(path).Length;

            long actual = await _fs.GetTotalSizeAsync([(path, false)]);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetTotalSizeAsync_EmptyList_ReturnsZero()
        {
            long size = await _fs.GetTotalSizeAsync([]);
            Assert.Equal(0, size);
        }

        [Fact]
        public async Task GetTotalSizeAsync_Directory_IncludesContents()
        {
            var dir = TempDir("d");
            File.WriteAllText(Path.Combine(dir, "x.txt"), "abc");
            File.WriteAllText(Path.Combine(dir, "y.txt"), "de");

            long size = await _fs.GetTotalSizeAsync([(dir, true)]);

            Assert.Equal(5, size);
        }

        [Fact]
        public async Task CreateDirectoryAsync_CreatesDirectory()
        {
            var path = Path.Combine(_root, "newdir");
            await _fs.CreateDirectoryAsync(path);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public async Task DeleteFileAsync_DeletesFile()
        {
            var path = TempFile("del.txt");
            await _fs.DeleteFileAsync(path);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public async Task DeleteDirectoryAsync_DeletesDirectoryWithContents()
        {
            var dir = TempDir("toDelete");
            File.WriteAllText(Path.Combine(dir, "inner.txt"), "data");
            // Make a hidden file to exercise the hidden-file branch
            var hiddenFile = Path.Combine(dir, "hidden.txt");
            File.WriteAllText(hiddenFile, "x");
            File.SetAttributes(hiddenFile, FileAttributes.Hidden);

            await _fs.DeleteDirectoryAsync(dir);

            Assert.False(Directory.Exists(dir));
        }

        [Fact]
        public async Task MoveFileAsync_MovesFile()
        {
            var src = TempFile("move_src.txt", "data");
            var dest = Path.Combine(_root, "move_dst.txt");

            await _fs.MoveFileAsync(src, dest);

            Assert.False(File.Exists(src));
            Assert.True(File.Exists(dest));
            Assert.Equal("data", File.ReadAllText(dest));
        }

        [Fact]
        public async Task CopyFileAsync_CopiesFileWithoutDelete()
        {
            var src = TempFile("copy_src.txt", "content");
            var dest = Path.Combine(_root, "copy_dst.txt");

            await _fs.CopyFileAsync(src, dest, delete: false, overwrite: false,
                progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

            Assert.True(File.Exists(src));
            Assert.True(File.Exists(dest));
            Assert.Equal("content", File.ReadAllText(dest));
        }

        [Fact]
        public async Task CopyFileAsync_MoveSemantics_DeletesSource()
        {
            var src = TempFile("move_src2.txt", "data");
            var dest = Path.Combine(_root, "move_dst2.txt");

            await _fs.CopyFileAsync(src, dest, delete: true, overwrite: false,
                progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

            Assert.False(File.Exists(src));
            Assert.Equal("data", File.ReadAllText(dest));
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesAllFiles()
        {
            var srcDir = TempDir("srcDir");
            File.WriteAllText(Path.Combine(srcDir, "f1.txt"), "alpha");
            var sub = Path.Combine(srcDir, "sub");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "f2.txt"), "beta");

            var destDir = Path.Combine(_root, "destDir");

            await _fs.CopyDirectoryAsync(srcDir, destDir, recursive: true, overwrite: false,
                progress: null, processedSize: 0, totalSize: 0, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(destDir, "f1.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "sub", "f2.txt")));
        }

        [Fact]
        public async Task GetDuplicatesAsync_FindsExistingTargetFiles()
        {
            var src = TempFile("dup.txt", "src");
            var dest = TempDir("destFolder");
            File.WriteAllText(Path.Combine(dest, "dup.txt"), "dst");

            var dupes = await _fs.GetDuplicatesAsync([(src, false)], dest);

            Assert.Single(dupes);
        }

        [Fact]
        public async Task GetDuplicatesAsync_NoDuplicates_ReturnsEmpty()
        {
            var src = TempFile("unique.txt");
            var dest = TempDir("empty_dest");

            var dupes = await _fs.GetDuplicatesAsync([(src, false)], dest);

            Assert.Empty(dupes);
        }

        [Fact]
        public async Task SearchAsync_FindsFileByPattern()
        {
            TempFile("report.txt");
            TempFile("image.png");
            var found = new System.Collections.Generic.List<string>();
            var progress = new Progress<string>(r => found.Add(r));

            await _fs.SearchAsync(_root, "*.txt", topOnly: true, searchContent: false,
                contentText: "", results: progress, statusProgress: null, CancellationToken.None);

            // Progress callbacks are async; wait briefly for them to flush
            await Task.Delay(100);
            Assert.Contains(found, f => f.EndsWith("report.txt"));
            Assert.DoesNotContain(found, f => f.EndsWith("image.png"));
        }
    }
}
