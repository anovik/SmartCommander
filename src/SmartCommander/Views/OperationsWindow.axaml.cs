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
            Closing += (s, e) =>
            {
                if (!e.IsProgrammatic)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }
    }
}
