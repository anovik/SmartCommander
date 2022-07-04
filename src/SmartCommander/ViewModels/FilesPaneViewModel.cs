using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace SmartCommander.ViewModels
{
    public enum SortingBy
    {
        SortingByName = 0,
        SortingByExt,
        SortingBySize,
        SortingByDate,        
    }

    public class FilesPaneViewModel : ViewModelBase
    {
        private string _currentDirectory;
        public string CurrentDirectory
        {
            get { return _currentDirectory; }
            set 
            { 
                _currentDirectory = value; 
                GetFilesFolders(CurrentDirectory, FoldersFilesList);
                this.RaisePropertyChanged("CurrentDirectory");
                this.RaisePropertyChanged("CurrentDirectoryInfo");
            }
        }

        private int _totalFiles = 0;
        private int _totalFolders = 0;

        public string CurrentDirectoryInfo
        {
            get { return string.Format("Files: {0}, folders: {1}.", _totalFiles, _totalFolders); }
        }

        public FileViewModel CurrentItem { get; set; }

        public SortingBy Sorting { get; set; } = SortingBy.SortingByName;

        public bool IsSelected { get; set; }

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();      

        public FilesPaneViewModel()
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        public void CellPointerPressed(object sender, object parameter)
        {

        }

        public void SortingStarted(object sender, object parameter)
        {

        }

        public void BeginningEdit(object sender, object parameter)
        {
            DataGridBeginningEditEventArgs args = parameter as DataGridBeginningEditEventArgs;
            if (args != null)
            {
                args.Cancel = true;
            }
        }

        public void DoubleTapped(object sender, object parameter)
        {
            if (CurrentItem.IsFolder)
            {
                CurrentDirectory = CurrentItem.FullName;
            }
            else
            { 
                // it is a file, open it
                new Process
                {
                    StartInfo = new ProcessStartInfo(CurrentItem.FullName)
                    {
                        UseShellExecute = true
                    }
                }.Start();
            }           
        }

        public void Execute(string command)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo(command)
                {
                    WorkingDirectory = CurrentDirectory,                    
                    UseShellExecute = true
                }
            }.Start();
        }

        public void Delete()
        {
            try
            {
                if (CurrentItem.IsFolder)
                {
                    // additional warning should be given in case it is not empty
                    Directory.Delete(CurrentDirectory, true);                    
                }
                else
                {                    
                    File.Delete(CurrentItem.FullName);
                }
                CurrentDirectory = CurrentDirectory;
            }
            catch
            {

            }
        }

        public void CreateNewFolder(string name)
        {
            if (Directory.Exists(name))
            {
                // give a warning
                return;
            }
            Directory.CreateDirectory(name);
        }

        private void GetFilesFolders(string dir, ObservableCollection<FileViewModel> filesFoldersList)
        {
            if (!Directory.Exists(dir))
                return;
            filesFoldersList.Clear();
            filesFoldersList.Add(new FileViewModel()
            {
                FullName = "..",
                IsFolder = true,
                Name = "..",
                Extension = "",
                Size = "Folder",
                DateCreated = DateTime.Now
            });
            string[] subdirectoryEntries = Directory.GetDirectories(dir);
            foreach (string subdirectory in subdirectoryEntries)
            {
                filesFoldersList.Add(new FileViewModel()
                {
                    FullName = subdirectory,
                    IsFolder = true,
                    Name = Path.GetFileName(subdirectory),
                    Extension = "",
                    Size = "Folder",
                    DateCreated = File.GetCreationTime(subdirectory)
                });
            }

            string[] fileEntries = Directory.GetFiles(dir);
            foreach (string fileName in fileEntries)
            {
                filesFoldersList.Add(new FileViewModel()
                {
                    FullName = fileName,
                    IsFolder = false,
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Extension = Path.GetExtension(fileName).TrimStart('.'),
                    Size = "",
                    DateCreated = File.GetCreationTime(fileName)
                }); ;
            }

            _totalFiles = subdirectoryEntries.Length;
            _totalFolders = fileEntries.Length;      
        }
    }
}
