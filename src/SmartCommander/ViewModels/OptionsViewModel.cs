using SmartCommander.Models;

namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        public bool IsCurrentDirectoryDisplayed 
        { 
            get { return OptionsModel.Instance.IsCurrentDirectoryDisplayed; }
            set { OptionsModel.Instance.IsCurrentDirectoryDisplayed = value; } 
        }

        public bool IsFunctionKeysDisplayed
        {
            get { return OptionsModel.Instance.IsFunctionKeysDisplayed; }
            set { OptionsModel.Instance.IsFunctionKeysDisplayed = value; }
        }

        public bool IsCommandLineDisplayed
        {
            get { return OptionsModel.Instance.IsCommandLineDisplayed; }
            set { OptionsModel.Instance.IsCommandLineDisplayed = value; }
        }

        public bool IsHiddenSystemFilesDisplayed
        {
            get { return OptionsModel.Instance.IsHiddenSystemFilesDisplayed; }
            set { OptionsModel.Instance.IsHiddenSystemFilesDisplayed = value; }
        }

        public bool SaveSettingsOnExit
        {
            get { return OptionsModel.Instance.SaveSettingsOnExit; }
            set { OptionsModel.Instance.SaveSettingsOnExit = value; }
        }

        public bool ConfirmationWhenDeleteNonEmpty
        {
            get { return OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty; }
            set { OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty = value; }
        }
    }
}
