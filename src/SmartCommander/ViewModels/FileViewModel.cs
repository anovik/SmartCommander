using ReactiveUI;
using SmartCommander.Assets;
using System;
using System.Drawing;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class FileViewModel : ViewModelBase
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
                Size = Resources.Folder;
                DateCreated = File.GetCreationTime(fullName);
                ImageSource = "Assets/folder.png";
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
                ImageSource = "Assets/file.png";
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

                string destination = "";
                
                if (IsFolder)
                {
                    destination = Path.Combine(Path.GetDirectoryName(FullName), value);
                    Directory.Move(FullName, destination);
                }
                else
                {
                    destination = Path.Combine(Path.GetDirectoryName(FullName), value + "." + Extension);
                    File.Move(FullName, destination);
                }
                name = value;
                FullName = destination;
                this.RaisePropertyChanged(nameof(Name));
                this.RaisePropertyChanged(nameof(FullName));
            }
        }

        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public bool IsFolder { get; set; } 

        public string? ImageSource { get; set; }
    }
}
