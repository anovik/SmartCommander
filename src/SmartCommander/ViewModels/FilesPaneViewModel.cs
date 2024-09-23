using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Assets;
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
using System.Threading.Tasks;
using System.Windows.Input;
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
        public event EventHandler? FocusChanged;

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set
            {
                _currentDirectory = value;
                GetFilesFolders(CurrentDirectory, FoldersFilesList);
                this.RaisePropertyChanged(nameof(CurrentDirectory));
                this.RaisePropertyChanged(nameof(CurrentDirectoryInfo));
            }
        }


        private MainWindowViewModel _mainVM;

        public string CurrentDirectoryInfo => string.Format(Resources.CurrentDirInfo, _totalFiles, _totalFolders);

        public FileViewModel? _currentItem;
        public FileViewModel? CurrentItem
        {
            get => _currentItem;
            set => this.RaiseAndSetIfChanged(ref _currentItem, value);
        }

        public List<FileViewModel> CurrentItems { get; set; } = new List<FileViewModel>();

        public bool IsUnzip => CurrentItems.Count > 0 && CurrentItems[0].Extension == "zip";


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
                this.RaisePropertyChanged(nameof(GridBorderBrush));

                if (value)
                {
                    FocusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }


        public static bool IsCurrentDirectoryDisplayed
        {
            get => OptionsModel.Instance.IsCurrentDirectoryDisplayed;
        }


        public static Brush SelectedBrush = new SolidColorBrush(Colors.LightSkyBlue);
        public static Brush NotSelectedBrush = new SolidColorBrush(Colors.Transparent);
        public Brush GridBorderBrush => IsSelected ? SelectedBrush : NotSelectedBrush;

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();

        public FilesPaneViewModel()
        {
            _mainVM = new MainWindowViewModel();
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
        }

        public FilesPaneViewModel(MainWindowViewModel mainVM, EventHandler focusHandler)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            ViewCommand = ReactiveCommand.Create(View);
            EditCommand = ReactiveCommand.Create(Edit);
            ZipCommand = ReactiveCommand.Create(Zip);
            UnzipCommand = ReactiveCommand.Create(Unzip);
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
            _mainVM = mainVM;
            FocusChanged += focusHandler;
        }

        public event Action<object, object>? ScrollToItemRequested;

        public void RequestScroll(object item, object? column)
        {
            ScrollToItemRequested?.Invoke(item, column!);
        }
        public ReactiveCommand<Unit, Unit>? ViewCommand { get; }
        public ReactiveCommand<Unit, Unit>? EditCommand { get; }
        public ReactiveCommand<Unit, Unit>? ZipCommand { get; }
        public ReactiveCommand<Unit, Unit>? UnzipCommand { get; }

        public Interaction<ViewerViewModel, ViewerViewModel?> ShowViewerDialog { get; }

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
                if (header == Resources.Name)
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
                if (header == Resources.Extension)
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
                if (header == Resources.Size)
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
                if (header == Resources.Date)
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
                args.Column.DisplayIndex != 1)
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

        public void SelectionChanged(object sender, object parameter)
        {
            SelectionChangedEventArgs? args = parameter as SelectionChangedEventArgs;
            if (args != null)
            {
                DataGrid? grid = args.Source as DataGrid;
                if (grid != null)
                {
                    // non-bindable property
                    CurrentItems = grid.SelectedItems.Cast<FileViewModel>().ToList();
                    ///grid.SelectedIndex = 0;
                }
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
            _= View(null);
        }

        public async Task View(Action<ButtonResult, object?>? resultAction)
        {
            if (CurrentItem == null)
                return;
            if (!CurrentItem.IsFolder)
            {
                if (Convert.ToUInt64(CurrentItem.Size) > 128*1024*1024)
                {
                    MessageBox_Show(resultAction, Resources.TooLargeSize, Resources.Alert, ButtonEnum.Ok);
                    return;
                }
                var copy = new ViewerViewModel(CurrentItem.FullName);
                await ShowViewerDialog.Handle(copy);
                resultAction?.Invoke(ButtonResult.Ok, null);
            }
            else
            {
                MessageBox_Show(resultAction, Resources.CantViewFolder, Resources.Alert, ButtonEnum.Ok);
            }
        }
        public void Edit()
        {
            Edit(null);
        }

        public void Edit(Action<ButtonResult, object?>? resultAction)
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
                resultAction?.Invoke(ButtonResult.Ok, null);
            }
            else
            {
                MessageBox_Show(resultAction, Resources.CantEditFolder, Resources.Alert, ButtonEnum.Ok);
            }
        }

        public void Zip()
        {
            _mainVM.Zip();
        }

        public void Unzip()
        {
            _mainVM.Unzip();
        }

        private void LaunchProcess(string program, string argument)
        {
            var process = new Process();
            process.StartInfo.FileName = "x-terminal-emulator"; // Use the default terminal emulator
            process.StartInfo.Arguments = $"-e {program} \"{argument}\""; // Specify the command to run in the new terminal window
            process.StartInfo.UseShellExecute = false; // Required to use the terminal emulator
            process.Start();
        }      

        public void Delete(FileViewModel? item)
        {
            try
            {
                if (item == null)
                {
                    return;
                }
                if (item.IsFolder)
                {
                    Utils.DeleteDirectoryWithHiddenFiles(item.FullName);                    
                }
                else
                {
                    File.Delete(item.FullName);
                }
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
                MessageBox_Show(null, Resources.FolderExists, Resources.Alert, ButtonEnum.Ok);
                return;
            }
            Directory.CreateDirectory(newFolder);
        }

        public void ProcessCurrentItem(bool goToParent = false)
        {       
            if (CurrentItem == null)
            {
                return;
            }

            if (goToParent)
            {
                if (Directory.GetParent(CurrentDirectory) == null)
                {
                    return;
                }

                var prevFolder = Path.GetFileName(CurrentDirectory);
                CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName : CurrentDirectory;
                CurrentItem = FoldersFilesList.First(f => f.Name == prevFolder);
                return;
            }

            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    var prevFolder = Path.GetFileName(CurrentDirectory);
                    CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName : CurrentDirectory;
                    CurrentItem= FoldersFilesList.First(f => f.Name == prevFolder);
                }
                else
                {
                    CurrentDirectory = CurrentItem.FullName;
                    CurrentItem = FoldersFilesList[0];
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

        public void NavigateToFileItem(string resultFilename)
        {
                var parent=Directory.GetParent(resultFilename);
                CurrentDirectory = parent!.FullName;
                CurrentItem= FoldersFilesList.First(f => f.FullName == resultFilename);
                RequestScroll(CurrentItem, null);
        }

        string? _selectedDrive;
        string? SelectedDrive
        {
            get { return _selectedDrive; }
            set 
            {           
                if (!Directory.Exists(value))
                {
                    MessageBox_Show(null, Resources.DriveNotAvailable, Resources.Alert, ButtonEnum.Ok); 
                    return;
                }

                FileInfo f = new FileInfo(CurrentDirectory);
                var driveFromDirectory = Path.GetPathRoot(f.FullName);

                _selectedDrive = value; 
                if (_selectedDrive != driveFromDirectory)
                {
                    CurrentDirectory = _selectedDrive;
                }
                this.RaisePropertyChanged(nameof(SelectedDrive)); 
            }
        }
    }
}
