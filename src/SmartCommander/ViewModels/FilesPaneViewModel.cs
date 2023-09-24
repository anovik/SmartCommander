using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Path = System.IO.Path;

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
        private string _currentDirectory = "";

        private int _totalFiles = 0;
        private int _totalFolders = 0;

        private bool _isSelected;
        private SortingBy _sorting = SortingBy.SortingByName;
        private bool _ascending = true;

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                _currentDirectory = value;
                GetFilesFolders(CurrentDirectory, FoldersFilesList);
                this.RaisePropertyChanged("CurrentDirectory");
                this.RaisePropertyChanged("CurrentDirectoryInfo");
            }
        }


        private MainWindowViewModel _mainVM;

        public string CurrentDirectoryInfo
        {
            get { return string.Format("Files: {0}, folders: {1}.", _totalFiles, _totalFolders); }
        }

        public FileViewModel? CurrentItem { get; set; }

        public List<FileViewModel> CurrentItems { get; set; }

        public SortingBy Sorting
        {
            get => _sorting;
            set
            {
                _sorting = value;
                Update();
            }
        }

        public bool Ascending
        {

            get => _ascending;
            set
            {
                _ascending = value;
                Update();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                this.RaisePropertyChanged("IsSelected");
                this.RaisePropertyChanged("GridBorderBrush");
            }
        }

      
        public bool IsCurrentDirectoryDisplayed
        {
            get => OptionsModel.Instance.IsCurrentDirectoryDisplayed;
        }


        public Brush GridBorderBrush => IsSelected ? new SolidColorBrush(Colors.LightSkyBlue) : new SolidColorBrush(Colors.Transparent);

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();

        public FilesPaneViewModel()
        {
            _mainVM = new MainWindowViewModel();
        }

        public FilesPaneViewModel(MainWindowViewModel mainVM)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);           
            ViewCommand = ReactiveCommand.Create(View);
            EditCommand = ReactiveCommand.Create(Edit);
            _mainVM = mainVM;           
        }   

        public ReactiveCommand<Unit, Unit>? EnterCommand { get; } 
        public ReactiveCommand<Unit, Unit>? ViewCommand { get; }
        public ReactiveCommand<Unit, Unit>? EditCommand { get; }

        public void CellPointerPressed(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void SortingStarted(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;

            DataGridColumnEventArgs? args = parameter as DataGridColumnEventArgs;
            if (args != null)
            {
                var header = args.Column.Header.ToString();
                if (header == "Name")
                {
                    if (Sorting == SortingBy.SortingByName)
                    {
                        Ascending = !Ascending;
                    }
                    else
                    {
                        Sorting = SortingBy.SortingByName;
                        Ascending = true;
                    }
                }
                if (header == "Extension")
                {
                    if (Sorting == SortingBy.SortingByExt)
                    {
                        Ascending = !Ascending;
                    }
                    else
                    {
                        Sorting = SortingBy.SortingByExt;
                        Ascending = true;
                    }
                }
                if (header == "Size")
                {
                    if (Sorting == SortingBy.SortingBySize)
                    {
                        Ascending = !Ascending;
                    }
                    else
                    {
                        Sorting = SortingBy.SortingBySize;
                        Ascending = true;
                    }
                }
                if (header == "Date")
                {
                    if (Sorting == SortingBy.SortingByDate)
                    {
                        Ascending = !Ascending;
                    }
                    else
                    {
                        Sorting = SortingBy.SortingByDate;
                        Ascending = true;
                    }
                }

                args.Handled = true;
            }
        }

        public void BeginningEdit(object sender, object parameter)
        {          
            DataGridBeginningEditEventArgs? args = parameter as DataGridBeginningEditEventArgs;          
            if (args != null && 
                args.Column.DisplayIndex != 0)
            {                
                args.Cancel = true;
            }
            if (args != null &&
                CurrentItem != null &&
                CurrentItem.FullName == "..")
            {
                args.Cancel = true;
            }      
        }    


        public void Tapped(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void DoubleTapped(object sender, object parameter)
        {          
            var args = parameter as TappedEventArgs;
            if (args != null)
            {
                var source = args.Source as Control;            
                if (source != null && 
                    (source.TemplatedParent is DataGridCell || source.Parent is DataGridCell))
                {                     
                    ProcessCurrentItem();
                }
            }
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

        public void Update()
        {
            // TODO: may be there is anything smarter?
            CurrentDirectory = CurrentDirectory;
        }

        public void View()
        {
            if (CurrentItem == null)
                return;
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LaunchProcess("less", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("LTFViewr5u.exe", CurrentItem.FullName);
                }
            }
            else
            {
                MessageBox_Show(null, "Can't view the folder", "Alert", ButtonEnum.Ok);
            }
        }

        public void Edit()
        {
            if (CurrentItem == null)
                return;
            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LaunchProcess("vi", CurrentItem.FullName);
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

        private void LaunchProcess(string program, string argument)
        {
            var process = new Process();
            process.StartInfo.FileName = "x-terminal-emulator"; // Use the default terminal emulator
            process.StartInfo.Arguments = $"-e {program} \"{argument}\""; // Specify the command to run in the new terminal window
            process.StartInfo.UseShellExecute = false; // Required to use the terminal emulator
            process.Start();
        }

        public bool NonEmptyFolder()
        {
            return OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty &&
                 CurrentItem!.IsFolder &&
                !IsDirectoryEmpty(CurrentItem!.FullName);
        }

        public void Delete()
        {
            try
            {
                if (CurrentItem == null)
                {
                    return;
                }
                if (CurrentItem.IsFolder)
                {
                    Directory.Delete(CurrentItem.FullName, true);
                }
                else
                {
                    File.Delete(CurrentItem.FullName);
                }
            }
            catch
            {
            }
        }

        public bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
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
        }

        private void ProcessCurrentItem()
        {       
            if (CurrentItem == null)
            {
                return;
            }
            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName :
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

        private void GetFilesFolders(string dir, IList<FileViewModel> filesFoldersList)
        {
            if (!Directory.Exists(dir) || !Path.IsPathFullyQualified(dir))
                return;
            filesFoldersList.Clear();
            _totalFolders = _totalFiles = 0;
            bool isParent = false;
            if (Directory.GetParent(CurrentDirectory) != null)
            {
                filesFoldersList.Add(new FileViewModel("..", true));
                isParent = true;
            }

            if (OperatingSystem.IsWindows())
            {
                FileInfo f = new FileInfo(CurrentDirectory);
                SelectedDrive = Path.GetPathRoot(f.FullName);
            }

            var options = new EnumerationOptions()
            {
                AttributesToSkip = OptionsModel.Instance.IsHiddenSystemFilesDisplayed ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            var subdirectoryEntries = Directory.EnumerateDirectories(dir, "*", options);
            var foldersList = new List<FileViewModel>();
            foreach (string subdirectory in subdirectoryEntries)
            {
                try
                {
                    foldersList.Add(new FileViewModel(subdirectory, true));
                    ++_totalFolders;
                }
                catch { }
            }

            var filesList = new List<FileViewModel>();
            var fileEntries = Directory.EnumerateFiles(dir, "*", options);
            foreach (string fileName in fileEntries)
            {
                try
                {
                    filesList.Add(new FileViewModel(fileName, false));
                    ++_totalFiles;
                }
                catch { }
            }
            if (Sorting == SortingBy.SortingByName)
            {
                if (Ascending)
                {
                    foldersList = foldersList.OrderBy(entry => entry.Name).ToList();
                    filesList = filesList.OrderBy(entry => entry.Name).ToList();
                }
                else
                {
                    foldersList = foldersList.OrderByDescending(entry => entry.Name).ToList();
                    filesList = filesList.OrderByDescending(entry => entry.Name).ToList();
                }
            }
            else if (Sorting == SortingBy.SortingByExt)
            {
                if (Ascending)
                {
                    foldersList = foldersList.OrderBy(entry => entry.Extension).ToList();
                    filesList = filesList.OrderBy(entry => entry.Extension).ToList();
                }
                else
                {
                    foldersList = foldersList.OrderByDescending(entry => entry.Extension).ToList();
                    filesList = filesList.OrderByDescending(entry => entry.Extension).ToList();
                }            
            }
            else if (Sorting == SortingBy.SortingBySize)
            {
                if (Ascending)
                {
                    foldersList = foldersList.OrderBy(entry => entry.Size).ToList();
                    filesList = filesList.OrderBy(entry => Convert.ToUInt64(entry.Size)).ToList();
                }
                else
                {
                    foldersList = foldersList.OrderByDescending(entry => entry.Size).ToList();
                    filesList = filesList.OrderByDescending(entry => Convert.ToUInt64(entry.Size)).ToList();
                }                
            }
            else if (Sorting == SortingBy.SortingByDate)
            {
                if (Ascending)
                {
                    foldersList = foldersList.OrderBy(entry => entry.DateCreated).ToList();
                    filesList = filesList.OrderBy(entry => entry.DateCreated).ToList();
                }
                else
                {
                    foldersList = foldersList.OrderByDescending(entry => entry.DateCreated).ToList();
                    filesList = filesList.OrderByDescending(entry => entry.DateCreated).ToList();
                }                
            }

            foreach (var folder in foldersList)
            {
                filesFoldersList.Add(folder);
            }

            foreach (var file in filesList)
            {
                filesFoldersList.Add(file);
            }

            if (filesFoldersList.Count > 0)
            {
                CurrentItem = (isParent && filesFoldersList.Count > 1) ?
                    filesFoldersList[1] : filesFoldersList[0];
            }
        }      

        string? _selectedDrive;
        string? SelectedDrive
        {
            get { return _selectedDrive; }
            set 
            {           
                if (!Directory.Exists(value))
                {
                    MessageBox_Show(null, "The drive is not available", "Alert", ButtonEnum.Ok); 
                    return;
                }

                FileInfo f = new FileInfo(CurrentDirectory);
                var driveFromDirectory = Path.GetPathRoot(f.FullName);

                _selectedDrive = value; 
                if (_selectedDrive != driveFromDirectory)
                {
                    CurrentDirectory = _selectedDrive;
                }
                this.RaisePropertyChanged("SelectedDrive"); 
            }
        }
    }
}
