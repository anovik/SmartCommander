using Avalonia.Controls;
using SmartCommander.ViewModels;

namespace SmartCommander.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // TODO: this will be changed later
            LeftPane.DataContext = new FilesPaneViewModel();
            RightPane.DataContext = new FilesPaneViewModel();
        }
    }
}
