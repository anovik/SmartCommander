using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class ProgressWindow : Window
    {
        // TODO: move Cancel to resources
        // TODO: process Cancel and exit -> call Cancel()
        // TODO: try to disable minimize
        // TODO: insert icon
        public ProgressWindow()
        {            
            InitializeComponent();
        }

        public void SetProgress(int value)
        {
            if (value < 0)
            {
                value = 0;
            }
            if (value > 100)
            {
                value = 100;
            }
            progressBar.Value = value;
        }
    }
}
