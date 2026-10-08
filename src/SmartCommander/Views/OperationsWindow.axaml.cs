using Avalonia.Controls;

namespace SmartCommander.Views
{
    // Closing this window (X) doesn't stop anything: operations keep running and each row has
    // its own Cancel. MainWindow opens a fresh instance when the next operation starts.
    public partial class OperationsWindow : Window
    {
        public OperationsWindow()
        {
            InitializeComponent();
        }
    }
}
