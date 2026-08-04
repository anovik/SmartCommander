using SmartCommander.ViewModels;
using Xunit;

namespace SmartCommander.Tests
{
    public class FtpTransferMenuModeTests
    {
        [Fact]
        public void NeitherPaneFtp_ReturnsLocalClipboard()
        {
            Assert.Equal(MainWindowViewModel.FtpTransferMenuMode.LocalClipboard,
                MainWindowViewModel.DetermineFtpTransferMenuMode(paneIsFtp: false, otherPaneIsFtp: false));
        }

        [Fact]
        public void ThisPaneFtp_ReturnsDownload()
        {
            Assert.Equal(MainWindowViewModel.FtpTransferMenuMode.Download,
                MainWindowViewModel.DetermineFtpTransferMenuMode(paneIsFtp: true, otherPaneIsFtp: false));
        }

        [Fact]
        public void OtherPaneFtp_ReturnsUpload()
        {
            Assert.Equal(MainWindowViewModel.FtpTransferMenuMode.Upload,
                MainWindowViewModel.DetermineFtpTransferMenuMode(paneIsFtp: false, otherPaneIsFtp: true));
        }
    }
}
