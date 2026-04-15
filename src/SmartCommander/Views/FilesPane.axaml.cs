using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
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
        static private Key[] gridhotkeys = [Key.Enter, Key.Back];

        public FilesPane()
        {

            InitializeComponent();

            if (OperatingSystem.IsWindows())
            {
                DataContextChanged += (s, e) =>
                {
                    if (DataContext is FilesPaneViewModel viewModel)
                    {
                        viewModel.ShowWindowsContextMenuInteraction.RegisterHandler(interaction =>
                        {
                            var topLevel = TopLevel.GetTopLevel(this);
                            if (topLevel != null)
                            {
                                try 
                                {
                                    IntPtr hwnd = IntPtr.Zero;
                                    if (topLevel.PlatformImpl is Avalonia.Platform.IPlatformHandle platformHandle)
                                    {
                                        hwnd = platformHandle.Handle;
                                    }
                                    
                                    if (hwnd == IntPtr.Zero)
                                    {
                                        var platformImpl = topLevel.PlatformImpl;
                                        if (platformImpl != null)
                                        {
                                            var handleProperty = platformImpl.GetType().GetProperty("Handle");
                                            if (handleProperty == null)
                                            {
                                                handleProperty = platformImpl.GetType().GetProperty("Handle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                            }

                                            var handleObject = handleProperty?.GetValue(platformImpl);
                                            if (handleObject is IntPtr ptr)
                                            {
                                                hwnd = ptr;
                                            }
                                            else if (handleObject is Avalonia.Platform.IPlatformHandle ph)
                                            {
                                                hwnd = ph.Handle;
                                            }
                                            else if (handleObject != null)
                                            {
                                                var innerHandleProperty = handleObject.GetType().GetProperty("Handle");
                                                if (innerHandleProperty != null)
                                                {
                                                    var val = innerHandleProperty.GetValue(handleObject);
                                                    if (val is IntPtr ptr2) hwnd = ptr2;
                                                }
                                            }
                                        }
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

                                        if (isBackground)
                                        {
                                            ShellContextMenuHelper.ShowBackgroundContextMenu(hwnd, interaction.Input[0]);
                                        }
                                        else
                                        {
                                            ShellContextMenuHelper.ShowContextMenu(hwnd, interaction.Input);
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("[DEBUG_LOG] Could not retrieve HWND from PlatformImpl.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] Error getting HWND: {ex}");
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
                focusManager = TopLevel.GetTopLevel((Visual)PaneDataGrid)?.FocusManager;
                PaneDataGrid.AddHandler(KeyDownEvent, dataGrid_PreviewKeyDown, RoutingStrategies.Tunnel);
                PaneDataGrid.ScrollIntoView(PaneDataGrid.SelectedItem, null);
                PaneDataGrid.Focus();
   
                var viewModel = (FilesPaneViewModel?)DataContext;
                viewModel!.ScrollToItemRequested += (item, column) =>
                {
                    PaneDataGrid.ScrollIntoView(item, (DataGridColumn)column);
                    PaneDataGrid.Focus();
                };
            }
        }



        public void dataGrid_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (gridhotkeys.Contains(e.Key) && ((focusManager?.GetFocusedElement() is DataGrid)))
            {
                var viewModel = DataContext as FilesPaneViewModel;
                if (e.Key == Key.Back)
                {
                    viewModel?.ProcessCurrentItem(true);
                }

                if (e.Key == Key.Enter)
                {
                    viewModel?.ProcessCurrentItem();
                }
            }
        }
        

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
