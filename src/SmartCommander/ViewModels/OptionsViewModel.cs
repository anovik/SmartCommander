using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Models;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
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
