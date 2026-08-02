using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class FtpConnectWindow : Window
    {
        public FtpConnectWindow()
        {
            InitializeComponent();
            Opened += (s, e) => HostTextBox.Focus();
        }
    }
}
