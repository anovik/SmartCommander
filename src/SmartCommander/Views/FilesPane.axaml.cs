using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Serilog;
using SmartCommander.Services;
using SmartCommander.ViewModels;
using System;
using System.Collections.Generic;
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

                // Tunnel + handledEventsToo: the DataGrid's cells handle PointerPressed themselves
                // (selection, begin-edit), and the drag gesture must see the press before that.
                PaneDataGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                PaneDataGrid.AddHandler(PointerMovedEvent, OnGridPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
                PaneDataGrid.AddHandler(PointerReleasedEvent, OnGridPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
                PaneDataGrid.AddHandler(PointerCaptureLostEvent, (s, args) => _dragPress = null, RoutingStrategies.Bubble, handledEventsToo: true);

                // Registered on the whole pane, not the DataGrid: the grid's empty area below the
                // last row isn't hit-testable, so a drag there lands on the pane's background Grid.
                DragDrop.SetAllowDrop(this, true);
                AddHandler(DragDrop.DragEnterEvent, OnDragOver);
                AddHandler(DragDrop.DragOverEvent, OnDragOver);
                AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
                AddHandler(DragDrop.DropEvent, OnDrop);
            }
        }

        // ---- Drag source ----

        // Slightly above the typical OS click-jitter so a normal click never starts a drag.
        private const double DragThreshold = 4;

        private PointerPressedEventArgs? _dragPress;
        private Point _dragPressPoint;
        private FileViewModel? _dragPressedItem;
        private List<FileViewModel> _selectionAtPress = new();
        private bool _pointerReleasedWhileStarting;

        private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragPress = null;
            if (paneDataGrid == null || isEditingCell ||
                !e.GetCurrentPoint(paneDataGrid).Properties.IsLeftButtonPressed ||
                (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta)) != 0)
            {
                return;
            }
            // Only a press on a real row arms a drag - not the header, scrollbars or "..".
            if (FindRowItem(e.Source as Visual) is not FileViewModel item || item.FullName == "..")
            {
                return;
            }
            _dragPress = e;
            _dragPressPoint = e.GetPosition(paneDataGrid);
            _dragPressedItem = item;
            // Snapshot before the DataGrid collapses a multi-selection to the pressed row (D6).
            _selectionAtPress = paneDataGrid.SelectedItems.OfType<FileViewModel>().ToList();
        }

        private void OnGridPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragPress == null || paneDataGrid == null)
            {
                return;
            }
            if (!e.GetCurrentPoint(paneDataGrid).Properties.IsLeftButtonPressed)
            {
                _dragPress = null;
                return;
            }
            var delta = e.GetPosition(paneDataGrid) - _dragPressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            {
                return;
            }
            var press = _dragPress;
            _dragPress = null;
            _ = StartDragAsync(press);
        }

        private void OnGridPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragPress = null;
            _pointerReleasedWhileStarting = true;
        }

        private async Task StartDragAsync(PointerPressedEventArgs press)
        {
            if (paneDataGrid == null || _dragPressedItem == null || DataContext is not FilesPaneViewModel viewModel)
            {
                return;
            }
            try
            {
                // The press on an already-current Name cell began an in-place rename; a drag wins.
                if (isEditingCell)
                {
                    paneDataGrid.CancelEdit();
                }

                var items = DragDropLogic.GetDragItems(_dragPressedItem, _selectionAtPress, i => i.FullName == "..");
                if (items.Count > 1)
                {
                    // Put back the selection the DataGrid collapsed on press, so what's highlighted
                    // is what's being dragged.
                    paneDataGrid.SelectedItems.Clear();
                    foreach (var item in items)
                    {
                        paneDataGrid.SelectedItems.Add(item);
                    }
                }

                _pointerReleasedWhileStarting = false;
                using var dataTransfer = await viewModel.BuildDragDataTransfer(items);
                // A release during the await would make the OS drag loop drop immediately
                // wherever the pointer happens to be.
                if (dataTransfer == null || _pointerReleasedWhileStarting)
                {
                    return;
                }
                // Copy|Move is offered so in-app drops can show a Move cursor; what actually
                // happens is decided by the drop handler, and the source pane never deletes
                // anything based on the returned effect.
                await DragDrop.DoDragDropAsync(press, dataTransfer, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Drag failed");
            }
        }

        // ---- Drop target ----

        private IDataTransfer? _cachedDropData;
        private List<string> _cachedDropPaths = new();
        private bool _cachedDropIsInApp;
        private DataGridRow? _highlightedRow;
        private long _lastAutoScrollTicks;

        private const double AutoScrollEdge = 24;
        private const long AutoScrollIntervalMs = 60;

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = ResolveDrop(e, out _, out var targetRow);
            SetDropHighlight(e.DragEffects == DragDropEffects.None ? null : targetRow,
                e.DragEffects != DragDropEffects.None);
            AutoScroll(e);
            e.Handled = true;
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            SetDropHighlight(null, false);
            _cachedDropData = null;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            SetDropHighlight(null, false);
            var effect = ResolveDrop(e, out var destDirectory, out _);
            // Paths are copied out now: the drag's data object is released once this returns.
            var sourcePaths = _cachedDropPaths.ToList();
            _cachedDropData = null;
            e.DragEffects = effect;
            e.Handled = true;
            if (effect == DragDropEffects.None || DataContext is not FilesPaneViewModel viewModel)
            {
                return;
            }
            // Fire-and-forget: the overwrite prompt and the operation itself must not run
            // inside the platform's modal drag loop.
            Dispatcher.UIThread.Post(() =>
                _ = viewModel.DropFiles(destDirectory, sourcePaths, effect == DragDropEffects.Move));
        }

        // Effect to show/apply for this drop, plus the directory it would land in and the folder
        // row (if any) that directory belongs to.
        private DragDropEffects ResolveDrop(DragEventArgs e, out string destDirectory, out DataGridRow? targetRow)
        {
            destDirectory = "";
            targetRow = null;
            if (paneDataGrid == null || DataContext is not FilesPaneViewModel viewModel)
            {
                return DragDropEffects.None;
            }

            CacheDropSources(e.DataTransfer);
            if (_cachedDropPaths.Count == 0)
            {
                return DragDropEffects.None;
            }

            destDirectory = viewModel.CurrentDirectory;
            var row = FindRow(paneDataGrid.InputHitTest(e.GetPosition(paneDataGrid)) as Visual);
            if (row?.DataContext is FileViewModel { IsFolder: true } folder && folder.FullName != "..")
            {
                destDirectory = folder.FullName;
                targetRow = row;
            }

            bool sourceIsFtp = RemotePath.IsFtp(_cachedDropPaths[0]);
            bool destIsFtp = RemotePath.IsFtp(destDirectory);
            // FTP only ever moves between the two panes in-app; OS<->FTP and FTP->FTP are out.
            if (((sourceIsFtp || destIsFtp) && !_cachedDropIsInApp) || (sourceIsFtp && destIsFtp))
            {
                return DragDropEffects.None;
            }
            if (DragDropLogic.IsNoOpDrop(_cachedDropPaths, destDirectory))
            {
                return DragDropEffects.None;
            }

            bool copyModifier = OperatingSystem.IsMacOS()
                ? e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                : e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool move = DragDropLogic.ResolveMove(copyModifier, e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                DragDropLogic.IsSameTransferRoot(_cachedDropPaths[0], destDirectory));

            // Respect what the source allows (e.g. an OS source that only offers Copy).
            var wanted = move ? DragDropEffects.Move : DragDropEffects.Copy;
            if ((e.DragEffects & wanted) != 0)
            {
                return wanted;
            }
            if ((e.DragEffects & DragDropEffects.Copy) != 0)
            {
                return DragDropEffects.Copy;
            }
            return (e.DragEffects & DragDropEffects.Move) != 0 ? DragDropEffects.Move : DragDropEffects.None;
        }

        // DragOver fires on every pointer move; read the source list once per drag.
        private void CacheDropSources(IDataTransfer data)
        {
            if (ReferenceEquals(data, _cachedDropData))
            {
                return;
            }
            _cachedDropData = data;
            _cachedDropPaths = new List<string>();
            _cachedDropIsInApp = false;
            try
            {
                var payload = data.TryGetValue(FilesPaneViewModel.PaneDragFormat);
                if (payload != null)
                {
                    _cachedDropIsInApp = true;
                    _cachedDropPaths = payload.Paths.ToList();
                    return;
                }
                _cachedDropPaths = (data.TryGetFiles() ?? [])
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read dragged files");
            }
        }

        private void SetDropHighlight(DataGridRow? row, bool accepting)
        {
            if (!ReferenceEquals(row, _highlightedRow))
            {
                _highlightedRow?.Classes.Remove("dropTarget");
                row?.Classes.Add("dropTarget");
                _highlightedRow = row;
            }
            // The whole-pane outline marks a drop into the current directory.
            var overlay = this.FindControl<Border>("DropHighlight");
            if (overlay != null)
            {
                overlay.IsVisible = accepting && row == null;
            }
        }

        private void AutoScroll(DragEventArgs e)
        {
            if (paneDataGrid == null || DataContext is not FilesPaneViewModel viewModel)
            {
                return;
            }
            long now = Environment.TickCount64;
            if (now - _lastAutoScrollTicks < AutoScrollIntervalMs)
            {
                return;
            }

            var pos = e.GetPosition(paneDataGrid);
            double headerHeight = paneDataGrid.GetVisualDescendants()
                .OfType<DataGridColumnHeadersPresenter>().FirstOrDefault()?.Bounds.Height ?? 0;
            double top = headerHeight;
            double bottom = paneDataGrid.Bounds.Height;
            int step;
            double probeY;
            if (pos.Y < top + AutoScrollEdge)
            {
                step = -1;
                probeY = top + AutoScrollEdge;
            }
            else if (pos.Y > bottom - AutoScrollEdge)
            {
                step = 1;
                probeY = bottom - AutoScrollEdge;
            }
            else
            {
                return;
            }

            if (FindRow(paneDataGrid.InputHitTest(new Point(AutoScrollEdge, probeY)) as Visual)?.DataContext
                is not FileViewModel edgeItem)
            {
                return;
            }
            int index = viewModel.FoldersFilesList.IndexOf(edgeItem) + step;
            if (index >= 0 && index < viewModel.FoldersFilesList.Count)
            {
                paneDataGrid.ScrollIntoView(viewModel.FoldersFilesList[index], null);
                _lastAutoScrollTicks = now;
            }
        }

        private static DataGridRow? FindRow(Visual? visual) =>
            visual as DataGridRow ?? visual?.FindAncestorOfType<DataGridRow>();

        private static FileViewModel? FindRowItem(Visual? visual) => FindRow(visual)?.DataContext as FileViewModel;

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
