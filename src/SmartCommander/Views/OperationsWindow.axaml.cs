using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class OperationsWindow : Window
    {
        public OperationsWindow()
        {
            InitializeComponent();
            // X hides the window while operations keep running (each row has its own Cancel;
            // X can't mean "cancel all" without being destructive). MainWindow re-shows it on
            // the next operation start and closes it programmatically on app shutdown.
            // Only a direct close of this window is intercepted: Avalonia asks owned windows
            // first when the owner closes, and cancelling an OwnerWindowClosing request here
            // would veto MainWindow's close before its own Closing (confirm dialog) ever ran.
            Closing += (s, e) =>
            {
                if (!e.IsProgrammatic && e.CloseReason == WindowCloseReason.WindowClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }
    }
}
