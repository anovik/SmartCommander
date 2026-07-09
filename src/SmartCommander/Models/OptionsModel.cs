using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using static System.Environment;

namespace SmartCommander.Models
{
    public class OptionsModel
    {
        public static OptionsModel Instance { get; } = new OptionsModel();
        private static readonly string _settingsDir = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "SmartCommander");
        private static readonly string _settingsPath = Path.Combine(_settingsDir, "settings.json");
        static OptionsModel()
        {
            Directory.CreateDirectory(_settingsDir);
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var options = JsonConvert.DeserializeObject<OptionsModel>(File.ReadAllText(_settingsPath));
                    if (options != null)
                    {
                        Instance = options;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load {SettingsPath}; falling back to default settings", _settingsPath);
                    try
                    {
                        File.Move(_settingsPath, _settingsPath + ".corrupt", overwrite: true);
                    }
                    catch (Exception moveEx)
                    {
                        Log.Error(moveEx, "Failed to move corrupt settings file {SettingsPath} aside", _settingsPath);
                    }
                }
            }
        }
        public void Save()
        {
            string tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            File.Move(tempPath, _settingsPath, overwrite: true);
        }


        public bool IsCurrentDirectoryDisplayed { get; set; } = true;

        public bool IsFunctionKeysDisplayed { get; set; } = true;

        public bool IsCommandLineDisplayed { get; set; } = true;

        public bool SaveWindowPositionSize { get; set; } = true;

        public bool IsHiddenSystemFilesDisplayed { get; set; }

        public bool SaveSettingsOnExit { get; set; } = true;

        public bool ConfirmationWhenDeleteNonEmpty { get; set; } = true;

        public double Top { get; set; } = -1;

        public double Left { get; set; } = -1;

        public double Width { get; set; } = -1;

        public double Height { get; set; } = -1;

        public bool IsMaximized { get; set; }

        public string LeftPanePath { get; set; } = "";

        public string RightPanePath { get; set; } = "";

        public bool IsDarkThemeEnabled { get; set; }
        public bool AllowOnlyOneInstance { get; set; } = true;
        public string Language { get; set; } = "en-US";

        public List<string> ListerPlugins { get; set; }= [];
    }
}
