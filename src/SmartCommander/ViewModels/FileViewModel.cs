using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SmartCommander.ViewModels
{
    public class FileViewModel : ViewModelBase
    {
        private string _name = "";
        private string _diskFullName = "";
        public static readonly List<string> ImageExtensions = ["jpg", "jpeg", "jpe", "bmp", "tiff", "gif", "png"];
        public static readonly List<string> VideoExtensions = ["mp4", "mov", "avi", "wmv"];
        public static readonly List<string> ArchiveExtensions = ["zip", "rar", "7z"];
        public static readonly List<string> DocumentExtensions = ["doc", "docx", "txt", "xlsx", "xls", "pdf"];
        public FileViewModel()
        {

        }

        public FileViewModel(string fullName, bool isFolder)
        {
            FullName = fullName;
            _diskFullName = fullName;
            IsFolder = isFolder;
            if (isFolder)
            {
                _name = Path.GetFileName(fullName);
                Extension = "";
                Size = Resources.Folder;
                DateCreated = File.GetCreationTime(fullName);
                ImageSource = "Assets/folder.png";
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(fullName)))
                {
                    _name = Path.GetFileName(fullName);
                    Extension = "";
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
        public string FullName { get; set; } = "";
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

                var oldName = _name;
                var oldFullName = _diskFullName;
                var destName = IsFolder ? value : (value + (!string.IsNullOrEmpty(Extension) ? "." + Extension : ""));
                var destination = Path.Combine(Path.GetDirectoryName(oldFullName) ?? "", destName);

                // Optimistic update so the DataGrid reflects the new name immediately
                _name = value;
                FullName = destination;
                this.RaisePropertyChanged(nameof(Name));
                this.RaisePropertyChanged(nameof(FullName));

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (IsFolder)
                        {
                            Directory.Move(oldFullName, destination);
                        }
                        else
                        {
                            File.Move(oldFullName, destination);
                        }
                        _diskFullName = destination;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Rename failed: {FullName}", oldFullName);
                        _diskFullName = oldFullName;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _name = oldName;
                            FullName = oldFullName;
                            this.RaisePropertyChanged(nameof(Name));
                            this.RaisePropertyChanged(nameof(FullName));
                        });
                    }
                });
            }
        }

        public string Extension { get; set; } = "";
        public string Size { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public bool IsFolder { get; set; }

        public string? ImageSource { get; set; }

        // Preferred factory for normal file/folder entries — uses IFileSystemService for metadata
        // so no sync I/O on the calling thread. Call from a background thread (e.g. inside Task.Run).
        public static async Task<FileViewModel> CreateAsync(string fullName, bool isFolder, IFileSystemService fs)
        {
            var vm = new FileViewModel
            {
                FullName = fullName,
                _diskFullName = fullName,
                IsFolder = isFolder,
            };

            if (isFolder)
            {
                vm._name = Path.GetFileName(fullName);
                vm.Extension = "";
                vm.Size = Resources.Folder;
                vm.DateCreated = await fs.GetCreationTimeAsync(fullName);
                vm.ImageSource = "Assets/folder.png";
            }
            else
            {
                (vm._name, vm.Extension) = ParseNameAndExtension(fullName);
                vm.Size = (await fs.GetFileSizeAsync(fullName)).ToString();
                vm.DateCreated = await fs.GetCreationTimeAsync(fullName);
                vm.ImageSource = SelectIconSource(vm.Extension);
            }

            return vm;
        }

        // Extracted for unit testing — pure path logic, no I/O.
        internal static (string Name, string Extension) ParseNameAndExtension(string fullName)
        {
            if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(fullName)))
            {
                return (Path.GetFileName(fullName), "");
            }
            return (Path.GetFileNameWithoutExtension(fullName), Path.GetExtension(fullName).TrimStart('.'));
        }

        // Extracted for unit testing — pure extension-to-icon mapping.
        internal static string SelectIconSource(string extension)
        {
            var ext = extension.ToLower();
            if (ImageExtensions.Contains(ext)) { return "Assets/image.png"; }
            if (VideoExtensions.Contains(ext)) { return "Assets/video.png"; }
            if (ArchiveExtensions.Contains(ext)) { return "Assets/archive.png"; }
            if (DocumentExtensions.Contains(ext)) { return "Assets/document.png"; }
            return "Assets/file.png";
        }
    }
}
