using SmartCommander.Plugins;
using System;
using System.Text;
using Xunit;

namespace SmartCommander.Tests
{
    public class DetectStringTests
    {
        private static bool Match(string detect, string file, long size = 100, string header = "", bool force = false)
        {
            var context = DetectContext.ForFile(file, size, Encoding.Latin1.GetBytes(header)) with { Force = force };
            return DetectString.Parse(detect).Matches(context);
        }

        // ---- SDK examples ----

        [Theory]
        [InlineData("a.wav", true)]
        [InlineData("a.avi", true)]
        [InlineData("a.mp3", false)]
        public void Sdk_ExtensionOr(string file, bool expected)
        {
            Assert.Equal(expected, Match("EXT=\"WAV\" | EXT=\"AVI\"", file));
        }

        [Fact]
        public void Sdk_RiffWaveHeader()
        {
            const string detect = "[0]=\"R\" & [1]=\"I\" & [2]=\"F\" & [3]=\"F\" & [8]=\"W\" & [9]=\"A\" & [10]=\"V\" & [11]=\"E\"";
            Assert.True(Match(detect, "x.bin", header: "RIFF\0\0\0\0WAVEfmt "));
            Assert.False(Match(detect, "x.bin", header: "RIFF\0\0\0\0AVI LIST"));
        }

        [Theory]
        [InlineData(999_999, false, true)]
        [InlineData(2_000_000, false, false)]
        [InlineData(2_000_000, true, true)]
        public void Sdk_SizeOrForce(long size, bool force, bool expected)
        {
            Assert.Equal(expected, Match("EXT=\"WAV\" & (SIZE<1000000 | FORCE)", "a.wav", size, force: force));
        }

        [Theory]
        [InlineData("PK\x03\x04rest", true)]
        [InlineData("PK\x07\x08rest", true)]
        [InlineData("PK\x05\x06rest", false)]
        public void Sdk_ZipTwoHeaders(string header, bool expected)
        {
            const string detect = "EXT=\"ZIP\" & ([0]=\"P\" & [1]=\"K\" & [2]=3 & [3]=4 | [0]=\"P\" & [1]=\"K\" & [2]=7 & [3]=8)";
            Assert.Equal(expected, Match(detect, "a.zip", header: header));
        }

        [Theory]
        [InlineData("just some notes", true)]
        [InlineData("<html><body>hi</body></html>", false)]
        [InlineData("<HTML><BODY>hi</BODY></HTML>", false)]
        public void Sdk_TextButNotHtml(string header, bool expected)
        {
            Assert.Equal(expected, Match("EXT=\"TXT\" & !(FINDI(\"<HTML\") | FINDI(\"<BODY\"))", "a.txt", header: header));
        }

        [Fact]
        public void Sdk_Multimedia_IsAlwaysTrue()
        {
            Assert.True(Match("MULTIMEDIA & (EXT=\"MP3\" | EXT=\"OGG\")", "a.mp3"));
            Assert.False(Match("MULTIMEDIA & (EXT=\"MP3\" | EXT=\"OGG\")", "a.txt"));
        }

        // ---- operators ----

        [Fact]
        public void And_BindsTighterThanOr()
        {
            // Read as EXT="A" | (EXT="B" & SIZE>1000), so a small .a still matches.
            Assert.True(Match("EXT=\"A\" | EXT=\"B\" & SIZE>1000", "x.a", size: 1));
            Assert.False(Match("EXT=\"A\" | EXT=\"B\" & SIZE>1000", "x.b", size: 1));
        }

        [Fact]
        public void Not_NegatesBracket()
        {
            Assert.True(Match("!(EXT=\"TXT\")", "a.log"));
            Assert.False(Match("!(EXT=\"TXT\")", "a.txt"));
        }

        [Fact]
        public void NotEqual_Works()
        {
            Assert.True(Match("EXT!=\"TXT\"", "a.log"));
            Assert.False(Match("EXT!=\"TXT\"", "a.txt"));
        }

        [Fact]
        public void Force_IsFalseByDefault()
        {
            Assert.False(Match("FORCE", "a.txt"));
        }

        // ---- header bytes ----

        [Fact]
        public void HeaderByte_BeyondHeader_NeverMatches()
        {
            Assert.False(Match("[5]=\"X\"", "a.bin", header: "ab"));
            Assert.False(Match("[5]!=\"X\"", "a.bin", header: "ab"));
        }

        [Fact]
        public void HeaderByte_ComparesAsNumber()
        {
            Assert.True(Match("[0]=80", "a.bin", header: "P"));
            Assert.True(Match("[0]>79 & [0]<81", "a.bin", header: "P"));
        }

        [Fact]
        public void NumberVersusLongString_IsNeverOrdered()
        {
            Assert.False(Match("SIZE<\"ABC\"", "a.bin"));
            Assert.False(Match("SIZE>\"ABC\"", "a.bin"));
            Assert.False(Match("SIZE=\"ABC\"", "a.bin"));
        }

        // ---- case handling ----

        [Fact]
        public void Ext_IsUppercasedAndCompareIsCaseInsensitive()
        {
            Assert.True(Match("EXT=\"JPG\"", "photo.jpg"));
            Assert.True(Match("EXT=\"jpg\"", "PHOTO.JPG"));
        }

        [Fact]
        public void Ext_NoExtension_IsEmpty()
        {
            Assert.True(Match("EXT=\"\"", "Makefile"));
            Assert.False(Match("EXT=\"TXT\"", "Makefile"));
        }

        [Fact]
        public void Keywords_AreCaseInsensitive()
        {
            Assert.True(Match("ext=\"TXT\" & size<1000", "a.txt"));
        }

        [Fact]
        public void Find_IsCaseSensitive_FindI_IsNot()
        {
            Assert.True(Match("FIND(\"Hello\")", "a.txt", header: "say Hello"));
            Assert.False(Match("FIND(\"hello\")", "a.txt", header: "say Hello"));
            Assert.True(Match("FINDI(\"hello\")", "a.txt", header: "say HELLO"));
        }

        [Fact]
        public void Find_EmptyHeader_NoMatch()
        {
            Assert.False(Match("FIND(\"x\")", "a.txt"));
        }

        // ---- malformed ----

        [Theory]
        [InlineData("")]
        [InlineData("EXT=")]
        [InlineData("EXT=\"TXT")]
        [InlineData("(EXT=\"TXT\"")]
        [InlineData("EXT=\"TXT\")")]
        [InlineData("[abc]=1")]
        [InlineData("[0=1")]
        [InlineData("FOO=1")]
        [InlineData("FIND(TXT)")]
        [InlineData("EXT=\"A\" & ")]
        [InlineData("EXT=\"A\" # 1")]
        [InlineData("!EXT=\"TXT\"")]
        [InlineData("!FORCE")]
        public void Malformed_Throws(string detect)
        {
            Assert.Throws<FormatException>(() => DetectString.Parse(detect));
        }
    }
}
