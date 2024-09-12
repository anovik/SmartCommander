using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;

namespace SmartCommander.Views
{
    public partial class FilesPane : UserControl
    {
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
            PaneDataGrid.Focus();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
