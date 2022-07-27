using System;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class FileViewModel
    {
        public FileViewModel()
        {

        }

        public FileViewModel(string fullName, bool isFolder)
        {
            FullName = fullName;
            IsFolder = isFolder;
            if (isFolder)
            {   
                Name = Path.GetFileName(fullName);
                Extension = "";
                Size = "Folder";
                DateCreated = File.GetCreationTime(fullName);
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(fullName)))
                {
                    Name = Path.GetFileName(fullName);
                    Extension = "";
                }
                else
                {
                    Name = Path.GetFileNameWithoutExtension(fullName);
                    Extension = Path.GetExtension(fullName).TrimStart('.');
                }
                Size = new FileInfo(fullName).Length.ToString();
                DateCreated = File.GetCreationTime(fullName);
            }
        }

        public string FullName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public bool IsFolder { get; set; }
    }
}
