using Avalonia.Controls;
using Avalonia.ReactiveUI;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        ProgressWindow progressWindow;
        public MainWindow() 
        {
            Opened += OnOpened;
            InitializeComponent();           

            this.WhenActivated(d => d(ViewModel!.ShowCopyDialog.RegisterHandler(DoShowCopyDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowOptionsDialog.RegisterHandler(DoShowOptionsDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowViewerDialog.RegisterHandler(DoShowViewerDialogAsync)));

            progressWindow = new ProgressWindow();

            Closing += async (s, e) =>
            {
                if (!e.IsProgrammatic)
                {
                    MainWindowViewModel? vm = DataContext as MainWindowViewModel;
                    if (vm != null)
                    {
                        if (vm.IsBackgroundOperation)
                        {
                            e.Cancel = true;
                            var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                            .GetMessageBoxStandard(Assets.Resources.Alert,
                                Assets.Resources.StopBackground + Environment.NewLine,
                                ButtonEnum.YesNo,
                                MsBox.Avalonia.Enums.Icon.Question);
                            var result = await messageBoxWindow.ShowAsPopupAsync(this);
                            if (result == ButtonResult.Yes)
                            {
                                vm.Cancel();
                                this.Close();
                            }
                            else
                            {
                                return;
                            }
                        }
                    }

                    progressWindow.Close();
                }
            };
        }

        private async Task DoShowViewerDialogAsync(InteractionContext<ViewerViewModel, ViewerViewModel?> interaction)
        {
            var dialog = new ViewerWindow();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<ViewerViewModel>(this);
            interaction.SetOutput(result);
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
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;                    
                    this.Arrange(new Avalonia.Rect(OptionsModel.Instance.Left, OptionsModel.Instance.Top, OptionsModel.Instance.Width, OptionsModel.Instance.Height));
                }
            }

            MainWindowViewModel? vm = DataContext as MainWindowViewModel;

            if (vm != null)
            {
                progressWindow.ViewModel = vm;
                LeftPane.DataContext = vm.LeftFileViewModel;
                RightPane.DataContext = vm.RightFileViewModel;

                vm.MessageBoxRequest += View_MessageBoxRequest;
                vm.LeftFileViewModel.MessageBoxRequest += View_MessageBoxRequest;
                vm.RightFileViewModel.MessageBoxRequest += View_MessageBoxRequest;

                vm.MessageBoxInputRequest += View_MessageBoxInputRequest;
                vm.LeftFileViewModel.MessageBoxInputRequest += View_MessageBoxInputRequest;
                vm.RightFileViewModel.MessageBoxInputRequest += View_MessageBoxInputRequest;

                vm.ProgressRequest += View_ProgressRequest;
            }
        }

        private void View_ProgressRequest(object? sender, int e)
        {   
            if (e == 0)
            {
                progressWindow.Show();
            }
            if (e >= 100)
            {
                progressWindow.Hide();
            }
            progressWindow.SetProgress(e);
        }

        void View_MessageBoxRequest(object? sender, MvvmMessageBoxEventArgs e)
        {
            e.Show(this);
        }

        void View_MessageBoxInputRequest(object? sender, MvvmMessageBoxEventArgs e)
        {
            e.ShowInput(this);
        }
    }
}
