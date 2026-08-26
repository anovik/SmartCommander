using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Serilog;
using SmartCommander.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public partial class FilesPane : UserControl
    {
        private IFocusManager? focusManager { get; set; }
        private DataGrid? paneDataGrid;
        private bool isEditingCell;
        static private Key[] gridhotkeys = [Key.Enter, Key.Back, Key.Tab];

        // Lets MainWindow focus the other pane's grid; the DataGrid otherwise consumes Tab internally for cell navigation.
        public event EventHandler? TabPressed;

        public FilesPane()
        {

            InitializeComponent();

            // Tunnel phase runs before the Grid/DataGrid's own ContextFlyout-opening handler marks the
            // event handled during the bubble phase, so this always gets a chance to run first.
            AddHandler(ContextRequestedEvent, OnContextRequested, RoutingStrategies.Tunnel);

            if (OperatingSystem.IsWindows())
            {
                DataContextChanged += (s, e) =>
                {
                    if (DataContext is FilesPaneViewModel viewModel && viewModel.ShowWindowsContextMenuInteraction != null)
                    {
                        viewModel.ShowWindowsContextMenuInteraction.RegisterHandler(interaction =>
                        {
                            var topLevel = TopLevel.GetTopLevel(this);
                            if (topLevel != null)
                            {
                                try 
                                {
                                    IntPtr hwnd = IntPtr.Zero;
                                    var platformHandle = topLevel.TryGetPlatformHandle();
                                    if (platformHandle != null)
                                    {
                                        hwnd = platformHandle.Handle;
                                    }

                                    if (hwnd != IntPtr.Zero)
                                    {
                                        bool isBackground = false;
                                        if (interaction.Input.Length == 1 && Directory.Exists(interaction.Input[0]))
                                        {
                                            if (DataContext is FilesPaneViewModel vm && interaction.Input[0] == vm.CurrentDirectory)
                                            {
                                                isBackground = true;
                                            }
                                        }

                                        if (OperatingSystem.IsWindows())
                                        {
                                            if (isBackground)
                                            {
                                                ShellContextMenuHelper.ShowBackgroundContextMenu(hwnd, interaction.Input[0]);
                                            }
                                            else
                                            {
                                                ShellContextMenuHelper.ShowContextMenu(hwnd, interaction.Input);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("Could not retrieve HWND from PlatformImpl");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error getting HWND");
                                }
                            }
                            interaction.SetOutput(Unit.Default);
                        });
                    }
                };

                var driveInfos = DriveInfo.GetDrives();
                ComboBox? comboBox = this.Find<ComboBox>("driveCombo");
                if (comboBox != null)
                {
                    comboBox.ItemsSource = driveInfos.Select(k => k.Name).ToList();
                    comboBox.SelectedIndex = 0;
                }
            }

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var PaneDataGrid = this.Get<DataGrid>("PaneDataGrid");
            if (PaneDataGrid != null)
            {
                paneDataGrid = PaneDataGrid;
                focusManager = TopLevel.GetTopLevel((Visual)PaneDataGrid)?.FocusManager;
                PaneDataGrid.AddHandler(KeyDownEvent, dataGrid_PreviewKeyDown, RoutingStrategies.Tunnel);
                // PreparingCellForEdit (not BeginningEdit) only fires once editing actually starts, so it can't race the ViewModel's cancellation.
                PaneDataGrid.PreparingCellForEdit += (s, args) => isEditingCell = true;
                PaneDataGrid.CellEditEnded += (s, args) => isEditingCell = false;
                PaneDataGrid.ScrollIntoView(PaneDataGrid.SelectedItem, null);
                PaneDataGrid.Focus();

                var viewModel = (FilesPaneViewModel?)DataContext;
                viewModel!.ScrollToItemRequested += (item, column) =>
                {
                    PaneDataGrid.ScrollIntoView(item, (DataGridColumn)column);
                    PaneDataGrid.Focus();
                };
                viewModel.RenameRequested += (s, args) => BeginRenameEdit();
            }
        }

        public void FocusGrid()
        {
            paneDataGrid?.Focus();
        }

        // Column 1 is always Name (matches the DisplayIndex check in
        // FilesPaneViewModel.BeginningEdit) - point the grid at it and start editing.
        private void BeginRenameEdit()
        {
            if (paneDataGrid == null)
            {
                return;
            }
            paneDataGrid.Focus();
            paneDataGrid.CurrentColumn = paneDataGrid.Columns[1];
            paneDataGrid.BeginEdit();
        }



        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (DataContext is FilesPaneViewModel viewModel)
            {
                _ = viewModel.UpdatePasteAvailability();
            }
        }

        public void dataGrid_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && isEditingCell)
            {
                // Focus is on the inline editor here, not the DataGrid, so commit explicitly and swallow Enter to skip the grid's default move-to-next-row.
                (sender as DataGrid)?.CommitEdit();
                e.Handled = true;
                return;
            }

            if (gridhotkeys.Contains(e.Key) && ((focusManager?.GetFocusedElement() is DataGrid)))
            {
                var viewModel = DataContext as FilesPaneViewModel;
                if (e.Key == Key.Back)
                {
                    _ = viewModel?.ProcessCurrentItem(true);
                }

                if (e.Key == Key.Enter)
                {
                    _ = viewModel?.ProcessCurrentItem();
                    e.Handled = true;
                }

                if (e.Key == Key.Tab)
                {
                    TabPressed?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            }
        }
        

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
