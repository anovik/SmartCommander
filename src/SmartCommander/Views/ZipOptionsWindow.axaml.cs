using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class ZipOptionsWindow : Window
    {
        public ZipOptionsWindow()
        {
            InitializeComponent();
            Opened += (s, e) => PasswordTextBox.Focus();
        }
    }
}
