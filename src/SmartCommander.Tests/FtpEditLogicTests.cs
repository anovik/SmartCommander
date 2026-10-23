using SmartCommander.ViewModels;
using System;
using Xunit;

namespace SmartCommander.Tests
{
    public class FtpEditLogicTests
    {
        private static readonly DateTime T0 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        private static readonly FileStamp Downloaded = new(T0, 100);
        private static readonly FileStamp Saved = new(T0.AddSeconds(30), 120);

        [Fact]
        public void IsSettledSave_Untouched_IsFalse()
        {
            Assert.False(FtpEditLogic.IsSettledSave(Downloaded, Downloaded, Downloaded));
        }

        [Fact]
        public void IsSettledSave_FirstPollAfterSave_WaitsForNextPoll()
        {
            Assert.False(FtpEditLogic.IsSettledSave(Downloaded, previous: Downloaded, current: Saved));
        }

        [Fact]
        public void IsSettledSave_SameOnTwoPolls_IsTrue()
        {
            Assert.True(FtpEditLogic.IsSettledSave(Downloaded, previous: Saved, current: Saved));
        }

        [Fact]
        public void IsSettledSave_StillBeingWritten_IsFalse()
        {
            var growing = Saved with { Size = Saved.Size + 4096 };
            Assert.False(FtpEditLogic.IsSettledSave(Downloaded, previous: Saved, current: growing));
        }

        [Fact]
        public void IsSettledSave_AlreadyHandled_IsFalse()
        {
            Assert.False(FtpEditLogic.IsSettledSave(handled: Saved, previous: Saved, current: Saved));
        }

        [Fact]
        public void IsSettledSave_SameSizeNewTimestamp_IsTrue()
        {
            var resaved = Downloaded with { Modified = T0.AddSeconds(5) };
            Assert.True(FtpEditLogic.IsSettledSave(Downloaded, previous: resaved, current: resaved));
        }

        [Fact]
        public void IsSettledSave_SameTimestampNewSize_IsTrue()
        {
            var resized = Downloaded with { Size = 101 };
            Assert.True(FtpEditLogic.IsSettledSave(Downloaded, previous: resized, current: resized));
        }
    }
}
