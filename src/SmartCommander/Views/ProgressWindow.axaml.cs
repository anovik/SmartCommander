using Avalonia.Controls;
using Avalonia.Interactivity;
using SmartCommander.ViewModels;
using SmartCommander.Assets;
using System;
using MsBox.Avalonia.Enums;
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
            if (!await Cancel())
            {
                e.Cancel = true;
            }
        }

        private async Task<bool> Cancel()
        {
            if (ViewModel != null && ViewModel.IsBackgroundOperation)
            {
                var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(Assets.Resources.Alert, 
                    Assets.Resources.StopBackground + Environment.NewLine, 
                    ButtonEnum.YesNoCancel, 
                    MsBox.Avalonia.Enums.Icon.Question);
                // TODO: test message box
                var result = await messageBoxWindow.ShowAsPopupAsync(this);
                if (result == ButtonResult.No)
                {
                    return false;
                }
                if (result == ButtonResult.Yes)
                {
                    ViewModel.Cancel();
                    Hide();
                }
            }
            return true;
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
