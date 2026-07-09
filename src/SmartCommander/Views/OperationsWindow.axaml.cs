using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class OperationsWindow : Window
    {
        public OperationsWindow()
        {
            InitializeComponent();
            // X hides rather than closes, since operations keep running and each row has its
            // own Cancel. Only a direct user close is intercepted (not IsProgrammatic), so this
            // doesn't veto MainWindow's own close when it closes this owned window on shutdown.
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
