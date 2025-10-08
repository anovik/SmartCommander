using Newtonsoft.Json;
using static System.Environment;
using System.IO;
using System.Collections.Generic;

namespace SmartCommander.Models
{
    public class Ftp
    {
        public string FtpName { get; set; }

        public bool IsAnonymous { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }

        public Ftp(string name)
        {
            FtpName = name;
        }
    }
    public class FtpModel
    {
        public static OptionsModel Instance { get; } = new OptionsModel();
        static readonly string _settingsDir = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "SmartCommander");
        static readonly string _settingsPath = Path.Combine(_settingsDir, "ftps.json");
        static FtpModel()
        {
            Directory.CreateDirectory(_settingsDir);
            if (File.Exists(_settingsPath))
            {
                var options = JsonConvert.DeserializeObject<OptionsModel>(File.ReadAllText(_settingsPath));
                if (options != null)
                {
                    Instance = options;
                }
            }
        }
        public void Save() => File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));

        public List<Ftp> Ftps { get; set; } = [];
        public Ftp? CurrentFtp { get; set; }

    }
}
