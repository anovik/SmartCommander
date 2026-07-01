using SmartCommander.ViewModels;
using System;
using System.IO;
using Xunit;

namespace SmartCommander.Tests
{
    public class DestinationInsideSourceTests
    {
        private static bool Check(string source, string dest) =>
            MainWindowViewModel.IsDestinationInsideSource(source, dest);

        private static char Sep => Path.DirectorySeparatorChar;

        [Fact]
        public void SameDirectory_ReturnsTrue()
        {
            Assert.True(Check($"C:{Sep}foo{Sep}bar", $"C:{Sep}foo{Sep}bar"));
        }

        [Fact]
        public void SameDirectory_TrailingSlash_ReturnsTrue()
        {
            Assert.True(Check($"C:{Sep}foo{Sep}bar{Sep}", $"C:{Sep}foo{Sep}bar"));
        }

        [Fact]
        public void Subdirectory_ReturnsTrue()
        {
            Assert.True(Check($"C:{Sep}foo", $"C:{Sep}foo{Sep}bar{Sep}baz"));
        }

        [Fact]
        public void ParentDirectory_ReturnsFalse()
        {
            Assert.False(Check($"C:{Sep}foo{Sep}bar", $"C:{Sep}foo"));
        }

        [Fact]
        public void SiblingDirectory_ReturnsFalse()
        {
            Assert.False(Check($"C:{Sep}foo{Sep}bar", $"C:{Sep}foo{Sep}baz"));
        }

        [Fact]
        public void CompletelyUnrelated_ReturnsFalse()
        {
            Assert.False(Check($"C:{Sep}alpha", $"D:{Sep}beta"));
        }

        [Fact]
        public void PrefixMatchWithoutSeparator_ReturnsFalse()
        {
            // "foo" should not match "foobar" as a subdirectory
            Assert.False(Check($"C:{Sep}foo", $"C:{Sep}foobar"));
        }

        [Fact]
        public void OnWindows_CaseInsensitive()
        {
            if (!OperatingSystem.IsWindows()) { return; }
            Assert.True(Check($"C:{Sep}FOO{Sep}BAR", $"C:{Sep}foo{Sep}bar{Sep}sub"));
        }
    }
}
