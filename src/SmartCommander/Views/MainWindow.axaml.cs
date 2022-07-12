using Avalonia.Controls;
using SmartCommander.ViewModels;
using System;

namespace SmartCommander.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Opened += OnOpened;
            InitializeComponent();
          
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            MainWindowViewModel vm = DataContext as MainWindowViewModel;

            if (vm != null)
            {
                LeftPane.DataContext = vm.LeftFileViewModel;
                RightPane.DataContext = vm.RightFileViewModel;

                vm.MessageBoxRequest += View_MessageBoxRequest;
                vm.LeftFileViewModel.MessageBoxRequest += View_MessageBoxRequest;
                vm.RightFileViewModel.MessageBoxRequest += View_MessageBoxRequest;

                vm.MessageBoxInputRequest += View_MessageBoxInputRequest;
                vm.LeftFileViewModel.MessageBoxInputRequest += View_MessageBoxInputRequest;
                vm.RightFileViewModel.MessageBoxInputRequest += View_MessageBoxInputRequest;

            }
        }

        async void View_MessageBoxRequest(object? sender, MvvmMessageBoxEventArgs e)
        {
            await e.Show(this);
        }

        async void View_MessageBoxInputRequest(object? sender,MvvmMessageBoxEventArgs e)
        {
            await e.ShowInput(this);
        }
    }
}
