using SmartCommander.ViewModels;
using Xunit;

namespace SmartCommander.Tests
{
    public class FileViewModelParsingTests
    {
        // ParseNameAndExtension

        [Fact]
        public void ParseNameAndExtension_FileWithExtension_SplitsCorrectly()
        {
            var (name, ext) = FileViewModel.ParseNameAndExtension("/home/user/document.txt");
            Assert.Equal("document", name);
            Assert.Equal("txt", ext);
        }

        [Fact]
        public void ParseNameAndExtension_HiddenFile_NoBaseName_ReturnsFullNameWithEmptyExtension()
        {
            // ".gitignore" has no base-name (GetFileNameWithoutExtension returns "")
            var (name, ext) = FileViewModel.ParseNameAndExtension("/repo/.gitignore");
            Assert.Equal(".gitignore", name);
            Assert.Equal("", ext);
        }

        [Fact]
        public void ParseNameAndExtension_MultiDotFile_UsesLastExtension()
        {
            var (name, ext) = FileViewModel.ParseNameAndExtension("/tmp/archive.tar.gz");
            Assert.Equal("archive.tar", name);
            Assert.Equal("gz", ext);
        }

        [Fact]
        public void ParseNameAndExtension_FileWithNoExtension_ReturnsEmptyExtension()
        {
            var (name, ext) = FileViewModel.ParseNameAndExtension("/usr/bin/grep");
            Assert.Equal("grep", name);
            Assert.Equal("", ext);
        }

        // SelectIconSource

        [Fact]
        public void SelectIconSource_JpgExtension_ReturnsImageIcon()
        {
            Assert.Equal("Assets/image.png", FileViewModel.SelectIconSource("jpg"));
        }

        [Fact]
        public void SelectIconSource_PngExtension_ReturnsImageIcon()
        {
            Assert.Equal("Assets/image.png", FileViewModel.SelectIconSource("PNG"));
        }

        [Fact]
        public void SelectIconSource_Mp4Extension_ReturnsVideoIcon()
        {
            Assert.Equal("Assets/video.png", FileViewModel.SelectIconSource("mp4"));
        }

        [Fact]
        public void SelectIconSource_ZipExtension_ReturnsArchiveIcon()
        {
            Assert.Equal("Assets/archive.png", FileViewModel.SelectIconSource("zip"));
        }

        [Fact]
        public void SelectIconSource_DocxExtension_ReturnsDocumentIcon()
        {
            Assert.Equal("Assets/document.png", FileViewModel.SelectIconSource("docx"));
        }

        [Fact]
        public void SelectIconSource_UnknownExtension_ReturnsFileIcon()
        {
            Assert.Equal("Assets/file.png", FileViewModel.SelectIconSource("xyz"));
        }

        [Fact]
        public void SelectIconSource_EmptyExtension_ReturnsFileIcon()
        {
            Assert.Equal("Assets/file.png", FileViewModel.SelectIconSource(""));
        }
    }
}
