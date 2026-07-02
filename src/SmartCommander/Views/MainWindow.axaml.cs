using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Serilog;
using SmartCommander.Models;
using SmartCommander.ViewModels;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        OperationsWindow operationsWindow;
        public MainWindow() 
        {
            Opened += OnOpened;
            InitializeComponent();

            this.WhenActivated(d => d(ViewModel!.ShowCopyDialog.RegisterHandler(
              interaction => DoShowDialogAsync<CopyMoveViewModel, CopyMoveWindow>(interaction)
            )));
            this.WhenActivated(d => d(ViewModel!.ShowOptionsDialog.RegisterHandler(
                interaction => DoShowDialogAsync<OptionsViewModel, OptionsWindow>(interaction)
            )));
            this.WhenActivated(d => d(ViewModel!.LeftFileViewModel.ShowViewerDialog.RegisterHandler(
                interaction => DoShowDialogAsync<ViewerViewModel, ViewerWindow>(interaction)
            )));
            this.WhenActivated(d => d(ViewModel!.RightFileViewModel.ShowViewerDialog.RegisterHandler(
                interaction => DoShowDialogAsync<ViewerViewModel, ViewerWindow>(interaction)
            )));
            this.WhenActivated(d => d(ViewModel!.ShowSearchDialog.RegisterHandler(
                interaction => DoShowDialogAsync<FileSearchViewModel, FileSearchWindow>(interaction)
            )));

            operationsWindow = new OperationsWindow();

            Closing += async (s, e) =>
            {
                try
                {
                    await HandleClosingAsync(e);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unhandled exception in MainWindow.Closing");
                }
            };
        }

        private async Task HandleClosingAsync(WindowClosingEventArgs e)
        {
            if (!e.IsProgrammatic)
            {
                MainWindowViewModel? vm = DataContext as MainWindowViewModel;
                if (vm != null)
                {
                    if (vm.ActiveOperations.Count > 0)
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
                            vm.CancelAllOperations();
                            this.Close();
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // Programmatic close bypasses the OperationsWindow hide-intercept.
                operationsWindow.Close();
            }
        }

        private async Task DoShowDialogAsync<T1, T2>(IInteractionContext<T1, T1?> interaction)
            where T2 : Window, new()
        {
            var dialog = new T2();
            dialog.DataContext = interaction.Input;
            dialog.Activate();

            var result = await dialog.ShowDialog<T1>(this);
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
                    Position = new PixelPoint((int)OptionsModel.Instance.Left, (int)OptionsModel.Instance.Top);
                    Width = OptionsModel.Instance.Width;
                    Height = OptionsModel.Instance.Height;
                }
            }

            MainWindowViewModel? vm = DataContext as MainWindowViewModel;

            if (vm != null)
            {
                operationsWindow.DataContext = vm;
                vm.ActiveOperations.CollectionChanged += OnActiveOperationsChanged;
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

        // Show/hide is driven by the ActiveOperations collection itself (the single source of
        // truth the window binds to), not by a separate progress event. The collection is only
        // mutated on the UI thread, so no marshaling is needed here.
        private void OnActiveOperationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Owned: stays above MainWindow without blocking it. Showing on every add also
                // re-surfaces the window if the user hid it with X while operations were running.
                operationsWindow.Show(this);
            }
            else if ((DataContext as MainWindowViewModel)?.ActiveOperations.Count == 0)
            {
                operationsWindow.Hide();
            }
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
