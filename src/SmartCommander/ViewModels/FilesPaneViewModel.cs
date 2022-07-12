using Avalonia.Controls;
using Avalonia.Input;
using MessageBox.Avalonia.Enums;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Runtime.InteropServices;

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

        private MainWindowViewModel _mainVM;

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
        }

        public FilesPaneViewModel(MainWindowViewModel mainVM)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            EnterCommand = ReactiveCommand.Create(Enter);
            _mainVM = mainVM;
        }

        public ReactiveCommand<Unit, Unit> EnterCommand { get; }

        public void CellPointerPressed(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void SortingStarted(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void BeginningEdit(object sender, object parameter)
        {
            DataGridBeginningEditEventArgs args = parameter as DataGridBeginningEditEventArgs;
            if (args != null)
            {
                args.Cancel = true;
            }
        }

        public void Tapped(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }


        public void Enter()
        {
            ProcessCurrentItem();
        }

        public void DoubleTapped(object sender, object parameter)
        {
            ProcessCurrentItem();
        }     
            
        public void Execute(string? command)
        {
            if (!string.IsNullOrEmpty(command))
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
        }

        public void View()
        {
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {                    
                    Process.Start("less", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // ? lister or whatever
                }
            }
            else
            {
                MessageBox_Show(null, "Can't view the folder", "Alert", ButtonEnum.Ok);
            }
        }

        public void Edit()
        {
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {                    
                    Process.Start("vi", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("notepad.exe", CurrentItem.FullName);
                }
            }
            else
            {
               MessageBox_Show(null, "Can't edit the folder", "Alert", ButtonEnum.Ok);
            }
        }

        public void Delete()
        {
            try
            {
                if (CurrentItem.IsFolder)
                {
                    // additional warning should be given in case it is not empty
                    Directory.Delete(CurrentItem.FullName, true);                    
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
            string newFolder = Path.Combine(CurrentDirectory, name);
            if (Directory.Exists(newFolder))
            {
                MessageBox_Show(null, "The folder already exists", "Alert", ButtonEnum.Ok);
                return;
            }
            Directory.CreateDirectory(newFolder);
            CurrentDirectory = CurrentDirectory;
        }

        private void ProcessCurrentItem()
        {
            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory).FullName :
                        CurrentDirectory;
                }
                else
                {
                    CurrentDirectory = CurrentItem.FullName;
                }
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

        private void GetFilesFolders(string dir, ObservableCollection<FileViewModel> filesFoldersList)
        {
            if (!Directory.Exists(dir))
                return;
            filesFoldersList.Clear();
            _totalFolders = _totalFiles = 0;
            bool isParent = false;
            if (Directory.GetParent(CurrentDirectory) != null)
            {
                filesFoldersList.Add(new FileViewModel()
                {
                    FullName = "..",
                    IsFolder = true,
                    Name = "..",
                    Extension = "",
                    Size = "Folder",
                    DateCreated = DateTime.Now
                });
                isParent = true;
            }

            var options = new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,                
            };

            var subdirectoryEntries = Directory.EnumerateDirectories(dir, "*", options);
            foreach (string subdirectory in subdirectoryEntries)
            {
                try
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
                    ++_totalFolders;
                }
                catch { }
            }

            var fileEntries = Directory.EnumerateFiles(dir, "*", options);
            foreach (string fileName in fileEntries)
            {
                try
                {
                    filesFoldersList.Add(new FileViewModel()
                    {
                        FullName = fileName,
                        IsFolder = false,
                        Name = Path.GetFileNameWithoutExtension(fileName),
                        Extension = Path.GetExtension(fileName).TrimStart('.'),
                        Size = new FileInfo(fileName).Length.ToString(),
                        DateCreated = File.GetCreationTime(fileName)
                    });
                    ++_totalFiles;
                }
                catch { }
            }     

            if (filesFoldersList.Count > 0)
            {
                CurrentItem = (isParent && filesFoldersList.Count > 1) ?
                    filesFoldersList[1] : filesFoldersList[0];
            }
        }
     
    }
}
