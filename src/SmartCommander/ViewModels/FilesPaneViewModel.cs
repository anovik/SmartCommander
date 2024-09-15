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
        public event EventHandler FocusChanged;

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


        private readonly MainWindowViewModel _mainVM;

        public string CurrentDirectoryInfo => string.Format(Resources.CurrentDirInfo, _totalFiles, _totalFolders);

        public FileViewModel? CurrentItem { get; set; }

        public List<FileViewModel> CurrentItems { get; set; } = [];

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
                this.RaisePropertyChanged("GridBorderBrush");

                if (value)
                {
                    FocusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }


        public bool IsCurrentDirectoryDisplayed => OptionsModel.Instance.IsCurrentDirectoryDisplayed;


        public static Brush SelectedBrush = new SolidColorBrush(Colors.LightSkyBlue);
        public static Brush NotSelectedBrush = new SolidColorBrush(Colors.Transparent);
        public Brush GridBorderBrush => IsSelected ? SelectedBrush : NotSelectedBrush;

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = [];

        public FilesPaneViewModel(EventHandler focusHandler)
        {
            _mainVM = new MainWindowViewModel();
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
            FocusChanged += focusHandler;
        }

        public FilesPaneViewModel(MainWindowViewModel mainVM, EventHandler focusHandler)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            ViewCommand = ReactiveCommand.Create(View);
            EditCommand = ReactiveCommand.Create(Edit);
            ZipCommand = ReactiveCommand.Create(Zip);
            UnzipCommand = ReactiveCommand.Create(Unzip);
            FilesPaneEnterCommand = ReactiveCommand.Create(() => ProcessCurrentItem());
            FilesPaneBackspaceCommand = ReactiveCommand.Create(() => ProcessCurrentItem(true));
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
            _mainVM = mainVM;
            FocusChanged += focusHandler;
        }

        public ReactiveCommand<Unit, Unit>? FilesPaneEnterCommand { get; }
        public ReactiveCommand<Unit, Unit>? FilesPaneBackspaceCommand { get; }
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

            if (parameter is DataGridColumnEventArgs args)
            {
                string? header = args.Column.Header.ToString();
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
            if (parameter is SelectionChangedEventArgs args)
            {
                if (args.Source is DataGrid grid)
                {
                    // non-bindable property
                    CurrentItems = grid.SelectedItems.Cast<FileViewModel>().ToList();
                }
            }
        }

        public void Tapped(object sender, object parameter)
        {
            _mainVM.SelectedPane = this;
        }

        public void DoubleTapped(object sender, object parameter)
        {
            if (parameter is TappedEventArgs args)
            {
                if (args.Source is Control source &&
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
                _ = new Process
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
            _ = View(null);
        }

        public async Task View(Action<ButtonResult, object?>? resultAction)
        {
            if (CurrentItem == null)
            {
                return;
            }

            if (!CurrentItem.IsFolder)
            {
                if (Convert.ToUInt64(CurrentItem.Size) > 128 * 1024 * 1024)
                {
                    MessageBox_Show(resultAction, Resources.TooLargeSize, Resources.Alert, ButtonEnum.Ok);
                    return;
                }
                ViewerViewModel copy = new(CurrentItem.FullName);
                _ = await ShowViewerDialog.Handle(copy);
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
            {
                return;
            }

            if (!CurrentItem.IsFolder)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LaunchProcess("vi", CurrentItem.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _ = Process.Start("notepad.exe", CurrentItem.FullName);
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
            Process process = new();
            process.StartInfo.FileName = "x-terminal-emulator"; // Use the default terminal emulator
            process.StartInfo.Arguments = $"-e {program} \"{argument}\""; // Specify the command to run in the new terminal window
            process.StartInfo.UseShellExecute = false; // Required to use the terminal emulator
            _ = process.Start();
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
            _ = Directory.CreateDirectory(newFolder);
        }

        private void ProcessCurrentItem(bool goToParent = false)
        {
            if (CurrentItem == null)
            {
                return;
            }

            if (goToParent)
            {
                CurrentDirectory = Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName :
                        CurrentDirectory;
                return;
            }

            if (CurrentItem.IsFolder)
            {
                CurrentDirectory = CurrentItem.FullName == ".."
                    ? Directory.GetParent(CurrentDirectory) != null ? Directory.GetParent(CurrentDirectory)!.FullName :
                        CurrentDirectory
                    : CurrentItem.FullName;
            }
            else
            {
                // it is a file, open it
                _ = new Process
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
            {
                return;
            }

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
                FileInfo f = new(CurrentDirectory);
                SelectedDrive = Path.GetPathRoot(f.FullName);
            }

            EnumerationOptions options = new()
            {
                AttributesToSkip = OptionsModel.Instance.IsHiddenSystemFilesDisplayed ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            IEnumerable<string> subdirectoryEntries = Directory.EnumerateDirectories(dir, "*", options);
            List<FileViewModel> foldersList = [];
            foreach (string subdirectory in subdirectoryEntries)
            {
                try
                {
                    foldersList.Add(new FileViewModel(subdirectory, true));
                    ++_totalFolders;
                }
                catch { }
            }

            List<FileViewModel> filesList = [];
            IEnumerable<string> fileEntries = Directory.EnumerateFiles(dir, "*", options);
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

            foreach (FileViewModel folder in foldersList)
            {
                filesFoldersList.Add(folder);
            }

            foreach (FileViewModel file in filesList)
            {
                filesFoldersList.Add(file);
            }

            if (filesFoldersList.Count > 0)
            {
                CurrentItem = (isParent && filesFoldersList.Count > 1) ?
                    filesFoldersList[1] : filesFoldersList[0];
            }
        }

        private string? _selectedDrive;

        private string? SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                if (!Directory.Exists(value))
                {
                    MessageBox_Show(null, Resources.DriveNotAvailable, Resources.Alert, ButtonEnum.Ok);
                    return;
                }

                FileInfo f = new(CurrentDirectory);
                string? driveFromDirectory = Path.GetPathRoot(f.FullName);

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
