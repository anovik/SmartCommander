using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Utils;
using ReactiveUI;
using SmartCommander.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {

        public ObservableCollection<CultureInfo> AvailableCultures { get; }

        public CultureInfo SelectedCulture { get; set; }


        public ObservableCollection<string> ListerPlugins { get; set; } = new();
        private string _selectedPlugin = string.Empty;
        public string SelectedPlugin
        {
            get => _selectedPlugin;
            set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
        }

        // Explicit list of locales this app ships translations for (one CultureInfo per
        // Assets/Resources*.resx file). Scanning CultureTypes.AllCultures and probing each of
        // .NET's ~800 known cultures for a satellite resource assembly froze the UI on first open.
        private static readonly string[] SupportedCultureNames =
        {
            "en-US",
            "fr-FR",
            "ru-RU",
            "sr-Cyrl-RS",
            "sr-Latn-ME",
        };

        private static IEnumerable<CultureInfo> GetAvailableCultures() =>
            SupportedCultureNames.Select(name => new CultureInfo(name));

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
            SelectedCulture = AvailableCultures.FirstOrDefault(x => x.Name == Model.Language) ?? 
                              AvailableCultures.FirstOrDefault() ?? 
                              CultureInfo.CurrentUICulture;

            ListerPlugins.AddRange(Model.ListerPlugins);
            AddFileCommand = ReactiveCommand.CreateFromTask<Window>(AddFile);
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

        public static FilePickerFileType ListerPluginsFilter { get; } = new("Lister Plugins (64bit)")
        {
            Patterns = ["*.wlx64"]
        };
        private async Task AddFile(Window window)
        {
            var topLevel = GetTopLevel();
            if (topLevel == null)
            {
                return;
            }
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose plugin",
                AllowMultiple = false,
                FileTypeFilter = [ListerPluginsFilter]
            });

            if (files?.Count >= 1)
            {
                var filename = files.First().Path.LocalPath;

                if (ListerPlugins.IndexOf(filename)==-1)
                    ListerPlugins.Add(filename);
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
            Model.ListerPlugins = ListerPlugins.ToList();

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
