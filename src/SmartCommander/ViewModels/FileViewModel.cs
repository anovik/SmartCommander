using System;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class FileViewModel
    {
        private string name = "";
        public FileViewModel()
        {

        }

        public FileViewModel(string fullName, bool isFolder)
        {
            FullName = fullName;
            IsFolder = isFolder;
            if (isFolder)
            {
                name = Path.GetFileName(fullName);
                Extension = "";
                Size = "Folder";
                DateCreated = File.GetCreationTime(fullName);
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(fullName)))
                {
                    name = Path.GetFileName(fullName);
                    Extension = "";
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(fullName);
                    Extension = Path.GetExtension(fullName).TrimStart('.');
                }
                Size = new FileInfo(fullName).Length.ToString();
                DateCreated = File.GetCreationTime(fullName);
            }
        }

        public string FullName { get; set; } = "";
        public string Name 
        { 
            get
            {
                return name;
            }
            set
            {
                if (string.IsNullOrEmpty(value) || value == name)
                {
                    return;
                }
                
                if (IsFolder)
                {
                    string destination = Path.Combine(Path.GetDirectoryName(FullName), value);
                    Directory.Move(FullName, destination);
                }
                else
                {
                    string destination = Path.Combine(Path.GetDirectoryName(FullName), value + "." + Extension);
                    File.Move(FullName, destination);
                }
            }
        }

        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public bool IsFolder { get; set; }
    }
}
