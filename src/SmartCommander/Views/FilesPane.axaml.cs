using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SmartCommander.ViewModels;
using System;
using System.IO;
using System.Linq;

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
