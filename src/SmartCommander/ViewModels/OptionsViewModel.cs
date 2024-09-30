using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Utils;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Resources;
using System.Threading.Tasks;

namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {

        public ObservableCollection<CultureInfo> AvailableCultures { get; }

        public CultureInfo SelectedCulture { get; set; }


        public ObservableCollection<string>? ListerPlugins { get; set; }
        private string _selectedPlugin = string.Empty;
        public string SelectedPlugin
        {
            get => _selectedPlugin;
            set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
        }

        private static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            List<CultureInfo> result = new List<CultureInfo>();
            ResourceManager rm = new ResourceManager(typeof(Resources));
            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

            foreach (CultureInfo culture in cultures)
            {
                if (culture.Equals(CultureInfo.InvariantCulture))
                {
                    result.Add(new CultureInfo("en-US"));
                    continue;
                }

                ResourceSet? rs = rm?.GetResourceSet(culture, true, false);
                if (rs != null)
                    result.Add(culture);
            }
            return result;
        }

        public OptionsViewModel()
        {
            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);

            IsCurrentDirectoryDisplayed = Model.IsCurrentDirectoryDisplayed;
            IsFunctionKeysDisplayed = Model.IsFunctionKeysDisplayed;
            IsCommandLineDisplayed = Model.IsCommandLineDisplayed;
            IsHiddenSystemFilesDisplayed = Model.IsHiddenSystemFilesDisplayed;
            SaveSettingsOnExit = Model.SaveSettingsOnExit;
            ConfirmationWhenDeleteNonEmpty = Model.ConfirmationWhenDeleteNonEmpty;
            SaveWindowPositionSize = Model.SaveWindowPositionSize;
            IsDarkThemeEnabled = Model.IsDarkThemeEnabled;
            AllowOnlyOneInstance = Model.AllowOnlyOneInstance;

            AvailableCultures = new ObservableCollection<CultureInfo>(GetAvailableCultures());
            var lang = AvailableCultures.First(x => x.Name == Model.Language);
            SelectedCulture = lang ?? AvailableCultures.First();
            ListerPlugins = new ObservableCollection<string>();
            ListerPlugins.AddRange(Model.ListerPlugins);
            AddFileCommand = ReactiveCommand.Create<Window>(AddFileAsync);
            RemoveFileCommand = ReactiveCommand.Create<Window>(RemoveFile);
        }

        public bool IsCurrentDirectoryDisplayed { get; set; }

        public bool IsFunctionKeysDisplayed { get; set; }

        public bool IsCommandLineDisplayed { get; set; }

        public bool IsHiddenSystemFilesDisplayed { get; set; }

        public bool SaveSettingsOnExit { get; set; }

        public bool ConfirmationWhenDeleteNonEmpty { get; set; }

        public bool SaveWindowPositionSize { get; set; }

        public bool IsDarkThemeEnabled { get; set; }
        public bool AllowOnlyOneInstance { get; set; }

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }
        public ReactiveCommand<Window, Unit> AddFileCommand { get; }
        public ReactiveCommand<Window, Unit> RemoveFileCommand { get; }
        private void RemoveFile(Window window)
        {
            if (!string.IsNullOrWhiteSpace(SelectedPlugin))
            {
                ListerPlugins?.Remove(SelectedPlugin);
            }
        }

        private void AddFileAsync(Window window)
        {
            var desktop = (IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;
            var topLevel = TopLevel.GetTopLevel(desktop?.MainWindow);

            // Start async operation to open the dialog.
            var files = topLevel?.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Text File",
                AllowMultiple = false,
            });

            if (files?.Result.Count >= 1)
            {
                ListerPlugins.Add(files.Result.FirstOrDefault().Name);
            }

        }

        public void SaveClose(Window window)
        {
            Model.IsCurrentDirectoryDisplayed = IsCurrentDirectoryDisplayed;
            Model.IsFunctionKeysDisplayed = IsFunctionKeysDisplayed;
            Model.IsCommandLineDisplayed = IsCommandLineDisplayed;
            Model.IsHiddenSystemFilesDisplayed = IsHiddenSystemFilesDisplayed;
            Model.SaveSettingsOnExit = SaveSettingsOnExit;
            Model.ConfirmationWhenDeleteNonEmpty = ConfirmationWhenDeleteNonEmpty;
            Model.SaveWindowPositionSize = SaveWindowPositionSize;
            Model.IsDarkThemeEnabled = IsDarkThemeEnabled;
            Model.Language = SelectedCulture.Name;
            Model.AllowOnlyOneInstance = AllowOnlyOneInstance;

            Model.Save();
            window?.Close(this);

        }

        public void Close(Window window)
        {
            window?.Close();
        }

        private OptionsModel Model => OptionsModel.Instance;
    }
}
