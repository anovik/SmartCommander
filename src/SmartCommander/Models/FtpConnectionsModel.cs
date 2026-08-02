using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Environment;

namespace SmartCommander.Models
{
    // A saved connection profile. Password is deliberately not a member here - it's never
    // persisted, the connect dialog always prompts for it.
    public class FtpConnectionProfile
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 21;
        public string Username { get; set; } = "";
        public bool Anonymous { get; set; }
    }

    public class FtpConnectionsModel
    {
        public static FtpConnectionsModel Instance { get; private set; } = new FtpConnectionsModel();
        private static readonly string _settingsDir = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "SmartCommander");
        private static readonly string _settingsPath = Path.Combine(_settingsDir, "ftpconnections.json");

        static FtpConnectionsModel()
        {
            Directory.CreateDirectory(_settingsDir);
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var model = JsonConvert.DeserializeObject<FtpConnectionsModel>(File.ReadAllText(_settingsPath));
                    if (model != null)
                    {
                        Instance = model;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load {SettingsPath}; falling back to no saved connections", _settingsPath);
                    try
                    {
                        File.Move(_settingsPath, _settingsPath + ".corrupt", overwrite: true);
                    }
                    catch (Exception moveEx)
                    {
                        Log.Error(moveEx, "Failed to move corrupt FTP connections file {SettingsPath} aside", _settingsPath);
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

        public List<FtpConnectionProfile> Connections { get; set; } = [];
    }
}
