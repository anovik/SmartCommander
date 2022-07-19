using Newtonsoft.Json;
using System.IO;
using static System.Environment;

namespace SmartCommander.Models
{
    public class OptionsModel
    {
        public static OptionsModel Instance { get; } = new OptionsModel();
        static string _settingsDir = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "SmartCommander");
        static string _settingsPath = Path.Combine(_settingsDir);
        static OptionsModel()
        {
            Directory.CreateDirectory(_settingsDir);
            if (File.Exists(_settingsPath))
                Instance = JsonConvert.DeserializeObject<OptionsModel>(File.ReadAllText(_settingsPath));
        }
        public void Save() => File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(this));


        public bool IsCurrentDirectoryDisplayed { get; set; } = true;

        public bool IsFunctionKeysDisplayed { get; set; } = true;

        public bool IsCommandLineDisplayed { get; set; } = true;

        public bool IsHiddenSystemFilesDisplayed { get; set; }

        public bool SaveSettingsOnExit { get; set; } = true;

        public bool ConfirmationWhenDeleteNonEmpty { get; set; } = true;
    }
}
