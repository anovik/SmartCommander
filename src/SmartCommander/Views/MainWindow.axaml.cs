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

        private void OnOpened(object sender, EventArgs e)
        {
            MainWindowViewModel vm = DataContext as MainWindowViewModel;

            if (vm != null)
            {
                LeftPane.DataContext = vm.LeftFileViewModel;
                RightPane.DataContext = vm.RightFileViewModel;
            }
        }
    }
}
