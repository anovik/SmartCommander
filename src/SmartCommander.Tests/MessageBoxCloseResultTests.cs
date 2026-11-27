using MsBox.Avalonia.Enums;
using SmartCommander.Views;
using Xunit;

namespace SmartCommander.Tests
{
    // A message box closed without a button (title-bar X) must report its cancel button,
    // so awaiting callers always continue and YesNoCancel callers never read it as No.
    public class MessageBoxCloseResultTests
    {
        [Theory]
        [InlineData(ButtonEnum.Ok, ButtonResult.Ok)]
        [InlineData(ButtonEnum.YesNo, ButtonResult.No)]
        [InlineData(ButtonEnum.OkCancel, ButtonResult.Cancel)]
        [InlineData(ButtonEnum.OkAbort, ButtonResult.Abort)]
        [InlineData(ButtonEnum.YesNoCancel, ButtonResult.Cancel)]
        [InlineData(ButtonEnum.YesNoAbort, ButtonResult.Abort)]
        public void CloseResult_IsTheCancelButton(ButtonEnum buttons, ButtonResult expected)
        {
            Assert.Equal(expected, MvvmMessageBoxEventArgs.GetCloseResult(buttons));
        }
    }
}
