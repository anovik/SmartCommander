using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class PasswordPromptWindow : Window
    {
        public PasswordPromptWindow()
        {
            InitializeComponent();
            Opened += (s, e) => PasswordTextBox.Focus();
        }
    }
}
