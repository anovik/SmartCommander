using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Serilog;
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
using System.Threading;
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
        private CancellationTokenSource? _loadCts;
        private string? _pendingRestoreItemName;
        private string? _pendingScrollTargetFullName;

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
                this.RaisePropertyChanged(nameof(CurrentDirectory));
                _ = LoadDirectoryAsync(value);
            }
        }


        private MainWindowViewModel _mainVM;

        public string CurrentDirectoryInfo => string.Format(Resources.CurrentDirInfo, _totalFiles, _totalFolders);

        private FileViewModel? _currentItem;
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


        public bool IsWindows => OperatingSystem.IsWindows();

        public static Brush SelectedBrush = new SolidColorBrush(Colors.LightSkyBlue);
        public static Brush NotSelectedBrush = new SolidColorBrush(Colors.Transparent);
        public Brush GridBorderBrush => IsSelected ? SelectedBrush : NotSelectedBrush;

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();

        public FilesPaneViewModel()
        {
            // Design-time only: avoid recursive construction chain
            _mainVM = null!;
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
        }

        public FilesPaneViewModel(MainWindowViewModel mainVM, EventHandler focusHandler)
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            ViewCommand = ReactiveCommand.Create(View);
            EditCommand = ReactiveCommand.Create(Edit);
            ZipCommand = ReactiveCommand.Create(Zip);
            UnzipCommand = ReactiveCommand.Create(Unzip);
            ShowMoreOptionsCommand = ReactiveCommand.CreateFromTask(ShowMoreOptions);
            ShowViewerDialog = new Interaction<ViewerViewModel, ViewerViewModel?>();
            ShowWindowsContextMenuInteraction = new Interaction<string[], Unit>();
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
        public ReactiveCommand<Unit, Unit>? ShowMoreOptionsCommand { get; }

        public Interaction<ViewerViewModel, ViewerViewModel?> ShowViewerDialog { get; }
        public Interaction<string[], Unit>? ShowWindowsContextMenuInteraction { get; }

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
            _ = View(null);
        }

        public async Task View(Action<ButtonResult, object?>? resultAction)
        {
            if (CurrentItem == null)
                return;
            if (!CurrentItem.IsFolder)
            {
                if (ulong.TryParse(CurrentItem.Size, out var fileSize) && fileSize > 128 * 1024 * 1024)
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

        public async Task ShowMoreOptions()
        {
            if (CurrentItems == null || CurrentItems.Count == 0)
            {
                if (CurrentItem != null)
                {
                    CurrentItems = new List<FileViewModel> { CurrentItem };
                }
                else
                {
                    // No items selected, show background context menu for current directory
                    if (ShowWindowsContextMenuInteraction != null)
                    {
                        await ShowWindowsContextMenuInteraction.Handle(new string[] { CurrentDirectory });
                    }
                    return;
                }
            }

            var paths = CurrentItems.Select(i => i.FullName).ToArray();
            if (ShowWindowsContextMenuInteraction != null)
            {
                await ShowWindowsContextMenuInteraction.Handle(paths);
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
            catch (Exception ex)
            {
                Log.Error(ex, "Delete failed: {FullName}", item?.FullName);
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
                var parent = Directory.GetParent(CurrentDirectory);
                _pendingRestoreItemName = prevFolder;
                CurrentDirectory = parent != null ? parent.FullName : CurrentDirectory;
                return;
            }

            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    var prevFolder = Path.GetFileName(CurrentDirectory);
                    var parent = Directory.GetParent(CurrentDirectory);
                    _pendingRestoreItemName = prevFolder;
                    CurrentDirectory = parent != null ? parent.FullName : CurrentDirectory;
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

        private async Task LoadDirectoryAsync(string dir)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            var cts = _loadCts = new CancellationTokenSource();

            var sortingBy = Sorting;
            var ascending = Ascending;

            List<FileViewModel> folders;
            List<FileViewModel> files;
            int totalFolders, totalFiles;
            bool isParent;
            string? selectedDrive;

            try
            {
                (folders, files, totalFolders, totalFiles, isParent, selectedDrive) =
                    await Task.Run(() => BuildEntries(dir, sortingBy, ascending, cts.Token), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load directory {Dir}", dir);
                return;
            }

            if (cts != _loadCts)
            {
                return;
            }

            _totalFolders = totalFolders;
            _totalFiles = totalFiles;
            FoldersFilesList.Clear();

            if (isParent)
            {
                FoldersFilesList.Add(new FileViewModel("..", true));
            }
            foreach (var f in folders)
            {
                FoldersFilesList.Add(f);
            }
            foreach (var f in files)
            {
                FoldersFilesList.Add(f);
            }

            if (FoldersFilesList.Count > 0)
            {
                CurrentItem = (isParent && FoldersFilesList.Count > 1)
                    ? FoldersFilesList[1] : FoldersFilesList[0];
            }
            else
            {
                CurrentItem = null;
            }

            if (_pendingRestoreItemName != null)
            {
                var restore = FoldersFilesList.FirstOrDefault(f => f.Name == _pendingRestoreItemName);
                if (restore != null)
                {
                    CurrentItem = restore;
                }
                _pendingRestoreItemName = null;
            }
            else if (_pendingScrollTargetFullName != null)
            {
                var target = FoldersFilesList.FirstOrDefault(f => f.FullName == _pendingScrollTargetFullName);
                if (target != null)
                {
                    CurrentItem = target;
                    RequestScroll(target, null);
                }
                _pendingScrollTargetFullName = null;
            }

            if (OperatingSystem.IsWindows())
            {
                SelectedDrive = selectedDrive;
            }

            this.RaisePropertyChanged(nameof(CurrentDirectoryInfo));
        }

        private static (List<FileViewModel> Folders, List<FileViewModel> Files, int TotalFolders, int TotalFiles, bool IsParent, string? SelectedDrive)
            BuildEntries(string dir, SortingBy sorting, bool ascending, CancellationToken ct)
        {
            if (!Directory.Exists(dir) || !Path.IsPathFullyQualified(dir))
            {
                return ([], [], 0, 0, false, null);
            }

            bool isParent = Directory.GetParent(dir) != null;

            string? selectedDrive = null;
            if (OperatingSystem.IsWindows())
            {
                selectedDrive = Path.GetPathRoot(new FileInfo(dir).FullName);
            }

            var options = new EnumerationOptions
            {
                AttributesToSkip = OptionsModel.Instance.IsHiddenSystemFilesDisplayed
                    ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            int totalFolders = 0, totalFiles = 0;
            var foldersList = new List<FileViewModel>();
            foreach (string subdirectory in Directory.EnumerateDirectories(dir, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    foldersList.Add(new FileViewModel(subdirectory, true));
                    ++totalFolders;
                }
                catch { }
            }

            var filesList = new List<FileViewModel>();
            foreach (string fileName in Directory.EnumerateFiles(dir, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    filesList.Add(new FileViewModel(fileName, false));
                    ++totalFiles;
                }
                catch { }
            }

            if (sorting == SortingBy.SortingByName)
            {
                foldersList = ascending
                    ? foldersList.OrderBy(e => e.Name).ToList()
                    : foldersList.OrderByDescending(e => e.Name).ToList();
                filesList = ascending
                    ? filesList.OrderBy(e => e.Name).ToList()
                    : filesList.OrderByDescending(e => e.Name).ToList();
            }
            else if (sorting == SortingBy.SortingByExt)
            {
                foldersList = ascending
                    ? foldersList.OrderBy(e => e.Extension).ToList()
                    : foldersList.OrderByDescending(e => e.Extension).ToList();
                filesList = ascending
                    ? filesList.OrderBy(e => e.Extension).ToList()
                    : filesList.OrderByDescending(e => e.Extension).ToList();
            }
            else if (sorting == SortingBy.SortingBySize)
            {
                foldersList = ascending
                    ? foldersList.OrderBy(e => e.Size).ToList()
                    : foldersList.OrderByDescending(e => e.Size).ToList();
                filesList = ascending
                    ? filesList.OrderBy(e => ulong.TryParse(e.Size, out var sz) ? sz : 0UL).ToList()
                    : filesList.OrderByDescending(e => ulong.TryParse(e.Size, out var sz) ? sz : 0UL).ToList();
            }
            else if (sorting == SortingBy.SortingByDate)
            {
                foldersList = ascending
                    ? foldersList.OrderBy(e => e.DateCreated).ToList()
                    : foldersList.OrderByDescending(e => e.DateCreated).ToList();
                filesList = ascending
                    ? filesList.OrderBy(e => e.DateCreated).ToList()
                    : filesList.OrderByDescending(e => e.DateCreated).ToList();
            }

            return (foldersList, filesList, totalFolders, totalFiles, isParent, selectedDrive);
        }

        public void NavigateToFileItem(string resultFilename)
        {
            var parent = Directory.GetParent(resultFilename);
            _pendingScrollTargetFullName = resultFilename;
            CurrentDirectory = parent!.FullName;
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
