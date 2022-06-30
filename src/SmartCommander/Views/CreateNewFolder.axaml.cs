using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SmartCommander.Views
{
    public partial class CreateNewFolder : Window
    {
        public CreateNewFolder()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
