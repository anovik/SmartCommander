namespace SmartCommander.ViewModels
{
    public class OptionsViewModel : ViewModelBase
    {
        public bool IsCurrentDirectoryDisplayed { get; set; } = true;

        public bool IsFunctionKeysDisplayed { get; set; } = true;        

        public bool IsCommandLineDisplayed { get; set; } = true;

        public bool IsHiddenSystemFilesDisplayed { get; set; }

        public bool SaveSettingsOnExit { get; set; } = true;

        public bool ConfirmationWhenDeleteNonEmpty { get; set; } = true;

        public void Save()
        {
            // serialize model
        }

        public void Load()
        {
            // deserialize model
        }
    }
}
