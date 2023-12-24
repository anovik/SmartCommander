using Avalonia.Controls;

namespace SmartCommander.Views
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            // TODO: move Operation in Progress to resources
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
