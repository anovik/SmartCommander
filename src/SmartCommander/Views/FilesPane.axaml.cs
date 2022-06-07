using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SmartCommander.Views
{
    public partial class FilesPane : UserControl
    {
        public FilesPane()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
