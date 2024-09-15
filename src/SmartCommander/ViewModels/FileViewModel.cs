using ReactiveUI;
using SmartCommander.Assets;
using System;
using System.Collections.Generic;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class FileViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        public static readonly List<string> ImageExtensions = new List<string>
                { "jpg", "jpeg", "jpe", "bmp", "tiff", "gif", "png" };
        public static readonly List<string> VideoExtensions = new List<string>
                { "mp4", "mov", "avi", "wmv" };
        public static readonly List<string> ArchiveExtensions = new List<string>
                { "zip", "rar", "7z" };
        public static readonly List<string> DocumentExtensions = new List<string>
                { "doc", "docx", "txt","xslx", "xsl", "pdf" };
        public FileViewModel()
        {

        }

        public FileViewModel(string fullName, bool isFolder)
        {
            FullName = fullName;
            IsFolder = isFolder;
            if (isFolder)
            {
                _name = Path.GetFileName(fullName);
                Extension = string.Empty;
                Size = Resources.Folder;
                DateCreated = File.GetCreationTime(fullName);
                ImageSource = "Assets/folder.png";
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(fullName)))
                {
                    _name = Path.GetFileName(fullName);
                    Extension = string.Empty;
                }
                else
                {
                    _name = Path.GetFileNameWithoutExtension(fullName);
                    Extension = Path.GetExtension(fullName).TrimStart('.');
                }
                Size = new FileInfo(fullName).Length.ToString();
                DateCreated = File.GetCreationTime(fullName);
                if (ImageExtensions.Contains(Extension.ToLower()))
                {
                    ImageSource = "Assets/image.png";
                }
                else if (VideoExtensions.Contains(Extension.ToLower()))
                {
                    ImageSource = "Assets/video.png";
                }
                else if (ArchiveExtensions.Contains(Extension.ToLower()))
                {
                    ImageSource = "Assets/archive.png";
                }
                else if (DocumentExtensions.Contains(Extension.ToLower()))
                {
                    ImageSource = "Assets/document.png";
                }
                else
                {
                    ImageSource = "Assets/file.png";
                }
            }
        }
        public string FullName { get; set; } = string.Empty;
        public string Name 
        { 
            get
            {
                return _name;
            }
            set
            {
                if (string.IsNullOrEmpty(value) || value == _name)
                {
                    return;
                }

                string destination = string.Empty;
                
                // moving here is fast since they are guaranteed to be on the same drive
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
                _name = value;
                FullName = destination;
                this.RaisePropertyChanged(nameof(Name));
                this.RaisePropertyChanged(nameof(FullName));
            }
        }

        public string Extension { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public bool IsFolder { get; set; } 

        public string? ImageSource { get; set; }
    }
}
