using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class CopyMoveWindow : Window
    {
        public CopyMoveWindow()
        {
            InitializeComponent();
            // Nothing else claims focus on open, so without this the default-button border wouldn't show until Tab.
            Opened += (s, e) => OkButton.Focus();
        }
    }
}
