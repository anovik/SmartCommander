using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System.IO;
using System.Linq;

namespace SmartCommander.Views
{
    public partial class FilesPane : UserControl
    {
        public FilesPane()
        {
            InitializeComponent();

            if (AvaloniaLocator.Current.GetService<IRuntimePlatform>().GetRuntimeInfo().OperatingSystem == OperatingSystemType.WinNT)
            {
                var driveInfos = DriveInfo.GetDrives();
                ComboBox comboBox = this.Find<ComboBox>("driveCombo");
                comboBox.Items = driveInfos.Select(k => k.Name).ToList();
                comboBox.SelectedIndex = 0;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
