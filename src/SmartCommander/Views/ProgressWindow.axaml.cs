using Avalonia.Controls;
using Avalonia.Interactivity;
using SmartCommander.ViewModels;

namespace SmartCommander.Views
{
    public partial class ProgressWindow : Window
    {                
        public MainWindowViewModel? ViewModel { get; set; }
        public ProgressWindow()
        {            
            InitializeComponent();
            Closing += ProgressWindow_Closing;
            cancelButton.Click += CancelButton_Click;           
        }


        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Cancel())
            {
                Hide();
            }
        }

        private void ProgressWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (ViewModel != null && ViewModel.Cancel())
            {
                Hide();
            }
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
