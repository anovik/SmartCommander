using SmartCommander.Services;
using Xunit;

namespace SmartCommander.Tests
{
    public class RemotePathTests
    {
        [Theory]
        [InlineData("ftp:///home/user", true)]
        [InlineData("FTP:///home/user", true)]
        [InlineData(@"C:\Users\anna", false)]
        [InlineData("/home/user", false)]
        public void IsFtp_DetectsScheme(string path, bool expected)
        {
            Assert.Equal(expected, RemotePath.IsFtp(path));
        }

        [Fact]
        public void GetRemotePart_ReturnsPathAfterScheme()
        {
            Assert.Equal("/home/user", RemotePath.GetRemotePart("ftp:///home/user"));
        }

        [Fact]
        public void GetRemotePart_AddsLeadingSlashIfMissing()
        {
            Assert.Equal("/home/user", RemotePath.GetRemotePart("ftp://home/user"));
        }

        [Fact]
        public void GetRemotePart_NotFtpPath_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => RemotePath.GetRemotePart(@"C:\Users\anna"));
        }

        [Fact]
        public void Combine_AddsSchemeAndLeadingSlash()
        {
            Assert.Equal("ftp:///home/user", RemotePath.Combine("/home/user"));
            Assert.Equal("ftp:///home/user", RemotePath.Combine("home/user"));
        }

        [Fact]
        public void CombineChild_FtpBase_JoinsWithForwardSlash()
        {
            Assert.Equal("ftp:///home/user/file.txt", RemotePath.CombineChild("ftp:///home/user", "file.txt"));
        }

        [Fact]
        public void CombineChild_FtpBaseWithTrailingSlash_DoesNotDoubleSlash()
        {
            Assert.Equal("ftp:///home/user/file.txt", RemotePath.CombineChild("ftp:///home/user/", "file.txt"));
        }

        [Fact]
        public void CombineChild_LocalBase_UsesPathCombine()
        {
            var expected = System.IO.Path.Combine(@"C:\Users\anna", "file.txt");
            Assert.Equal(expected, RemotePath.CombineChild(@"C:\Users\anna", "file.txt"));
        }
    }
}
