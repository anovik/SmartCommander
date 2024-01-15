using Avalonia.Controls;
using Avalonia.Interactivity;
using MsBox.Avalonia.Enums;
using SmartCommander.ViewModels;
using System;
using System.Threading.Tasks;

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


        private async void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            await Cancel();
        }

        private async void ProgressWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!e.IsProgrammatic)
            {
                e.Cancel = true;
                await Cancel();
            }           
        }

        private async Task Cancel()
        {
            if (ViewModel != null && ViewModel.IsBackgroundOperation)
            {
                var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Alert, 
                    Assets.Resources.StopBackground + Environment.NewLine, 
                    ButtonEnum.YesNo, 
                    MsBox.Avalonia.Enums.Icon.Question);
                var result = await messageBoxWindow.ShowAsync();       
                if (result == ButtonResult.Yes)
                {
                    ViewModel.Cancel();
                    Hide();
                }
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
