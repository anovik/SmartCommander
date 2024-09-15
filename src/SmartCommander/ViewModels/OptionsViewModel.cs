using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Resources;

namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {

        public ObservableCollection<CultureInfo> AvailableCultures { get; }

        public CultureInfo SelectedCulture { get; set; }

        private static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            List<CultureInfo> result = new List<CultureInfo>();
            ResourceManager rm = new ResourceManager(typeof(Resources));
            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

            foreach (CultureInfo culture in cultures)
            {
                    if (culture.Equals(CultureInfo.InvariantCulture)) {
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

            AvailableCultures = new ObservableCollection<CultureInfo>(GetAvailableCultures());
            var lang = AvailableCultures.FirstOrDefault(x => x.Name == Model.Language);
            SelectedCulture = lang is null ? AvailableCultures.FirstOrDefault() : lang;
        }

        public bool IsCurrentDirectoryDisplayed { get; set; }       

        public bool IsFunctionKeysDisplayed { get; set; }

        public bool IsCommandLineDisplayed { get; set; }

        public bool IsHiddenSystemFilesDisplayed { get; set; }

        public bool SaveSettingsOnExit { get; set; }

        public bool ConfirmationWhenDeleteNonEmpty { get; set; }

        public bool SaveWindowPositionSize { get; set; }

        public bool IsDarkThemeEnabled { get; set; }        

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

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

            Model.Save();
            if (window != null)
            {
                window.Close(this);
            }
        }

        public void Close(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        private OptionsModel Model => OptionsModel.Instance;
    }
}
