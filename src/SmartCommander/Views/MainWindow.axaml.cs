using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow() 
        {
            Opened += OnOpened;
            InitializeComponent();

            this.WhenActivated(d => d(ViewModel!.ShowCopyDialog.RegisterHandler(DoShowCopyDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowOptionsDialog.RegisterHandler(DoShowOptionsDialogAsync)));

           
        }        

        private async Task DoShowCopyDialogAsync(InteractionContext<CopyMoveViewModel, CopyMoveViewModel?> interaction)
        {
            var dialog = new CopyMoveWindow();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<CopyMoveViewModel>(this);
            interaction.SetOutput(result);
        }

        private async Task DoShowOptionsDialogAsync(InteractionContext<OptionsViewModel, OptionsViewModel?> interaction)
        {
            var dialog = new OptionsWindow();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<OptionsViewModel>(this);
            interaction.SetOutput(result);
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            if (OptionsModel.Instance.SaveWindowPositionSize &&
                OptionsModel.Instance.Left > -1 &&
                OptionsModel.Instance.Width > -1 &&
                OptionsModel.Instance.Top > -1 &&
                OptionsModel.Instance.Height > -1)
            {               
                if (OptionsModel.Instance.IsMaximized)
                {
                    WindowState = Avalonia.Controls.WindowState.Maximized;
                }
                else
                {
                    WindowState = Avalonia.Controls.WindowState.Normal;                    
                    this.Arrange(new Avalonia.Rect(OptionsModel.Instance.Left, OptionsModel.Instance.Top, OptionsModel.Instance.Width, OptionsModel.Instance.Height));
                }
            }

            MainWindowViewModel? vm = DataContext as MainWindowViewModel;

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
