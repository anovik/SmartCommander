using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Models;
using SmartCommander.Services;
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
        private readonly IFileSystemService _fs = null!;
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

        private bool _canPaste;
        public bool CanPaste
        {
            get => _canPaste;
            private set => this.RaiseAndSetIfChanged(ref _canPaste, value);
        }

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

        public static bool IsCurrentDirectoryDisplayed => OptionsModel.Instance.IsCurrentDirectoryDisplayed;

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

        public FilesPaneViewModel(MainWindowViewModel mainVM, EventHandler focusHandler, IFileSystemService fs)
        {
            _fs = fs;
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            ViewCommand = ReactiveCommand.Create(View);
            EditCommand = ReactiveCommand.Create(Edit);
            ZipCommand = ReactiveCommand.CreateFromTask(Zip);
            UnzipCommand = ReactiveCommand.CreateFromTask(Unzip);
            CopyCommand = ReactiveCommand.CreateFromTask(Copy);
            CutCommand = ReactiveCommand.CreateFromTask(Cut);
            PasteCommand = ReactiveCommand.CreateFromTask(Paste, this.WhenAnyValue(x => x.CanPaste));
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
        public ReactiveCommand<Unit, Unit>? CopyCommand { get; }
        public ReactiveCommand<Unit, Unit>? CutCommand { get; }
        public ReactiveCommand<Unit, Unit>? PasteCommand { get; }
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
            {
                return;
            }
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
                    Process.Start("notepad.exe", CurrentItem.FullName);
                }
                resultAction?.Invoke(ButtonResult.Ok, null);
            }
            else
            {
                MessageBox_Show(resultAction, Resources.CantEditFolder, Resources.Alert, ButtonEnum.Ok);
            }
        }

        public Task Zip()
        {
            return _mainVM.Zip();
        }

        public Task Unzip()
        {
            return _mainVM.Unzip();
        }

        // Cut/copy intent travels with the clipboard payload itself (rather than app-local state)
        // so a paste always reflects whatever is actually on the clipboard right now — including
        // clipboard content replaced by another application after an in-app Cut.
        private static readonly DataFormat<string> CutMarkerFormat =
            DataFormat.CreateStringApplicationFormat("SmartCommander.Cut");

        private static (IClipboard? Clipboard, IStorageProvider? StorageProvider) GetClipboardServices()
        {
            var topLevel = GetTopLevel();
            return (topLevel?.Clipboard, topLevel?.StorageProvider);
        }

        private async Task CopyOrCutToClipboard(bool isCut)
        {
            var items = (CurrentItems.Count > 0 ? CurrentItems :
                (CurrentItem != null ? new List<FileViewModel> { CurrentItem } : new List<FileViewModel>()))
                .Where(i => i.FullName != "..")
                .ToList();
            if (items.Count == 0)
            {
                return;
            }

            var (clipboard, storageProvider) = GetClipboardServices();
            if (clipboard == null || storageProvider == null)
            {
                return;
            }

            var dataTransfer = new DataTransfer();
            foreach (var item in items)
            {
                // Uri's implicit file-path detection only recognizes Windows drive-letter/UNC
                // forms; a plain absolute Unix path (e.g. "/home/user/file") has no scheme and
                // throws UriFormatException, so the file:// URI is built explicitly instead.
                var uri = new UriBuilder { Scheme = Uri.UriSchemeFile, Host = "", Path = item.FullName }.Uri;
                IStorageItem? storageItem = item.IsFolder
                    ? await storageProvider.TryGetFolderFromPathAsync(uri)
                    : await storageProvider.TryGetFileFromPathAsync(uri);
                if (storageItem != null)
                {
                    dataTransfer.Add(DataTransferItem.Create(DataFormat.File, storageItem));
                }
            }

            if (dataTransfer.Items.Count == 0)
            {
                return;
            }

            if (isCut)
            {
                dataTransfer.Add(DataTransferItem.Create(CutMarkerFormat, "1"));
            }

            await clipboard.SetDataAsync(dataTransfer);
            CanPaste = true;
        }

        public async Task UpdatePasteAvailability()
        {
            var (clipboard, _) = GetClipboardServices();
            if (clipboard == null)
            {
                CanPaste = false;
                return;
            }

            try
            {
                var formats = await clipboard.GetDataFormatsAsync();
                CanPaste = formats.Contains(DataFormat.File);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to query clipboard formats");
                CanPaste = false;
            }
        }

        public async Task Copy()
        {
            await CopyOrCutToClipboard(false);
        }

        public async Task Cut()
        {
            await CopyOrCutToClipboard(true);
        }

        public async Task Paste()
        {
            // Captured up front so a pane navigation while the clipboard read below is in
            // flight can't redirect the paste to a directory the user didn't intend.
            var destDirectory = CurrentDirectory;

            var (clipboard, _) = GetClipboardServices();
            if (clipboard == null)
            {
                return;
            }

            using var dataTransfer = await clipboard.TryGetDataAsync();
            if (dataTransfer == null)
            {
                return;
            }

            var files = await dataTransfer.TryGetFilesAsync();
            if (files == null || files.Length == 0)
            {
                return;
            }

            var sourcePaths = files
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .ToList();

            if (sourcePaths.Count == 0)
            {
                return;
            }

            bool isCut = dataTransfer.Contains(CutMarkerFormat);
            bool proceeded = await _mainVM.PasteFiles(destDirectory, sourcePaths, isCut);
            if (isCut && proceeded)
            {
                // A cut is a one-time move: clear the clipboard so a stray repeat Ctrl+V
                // doesn't retry the operation against the now-deleted source. Only clear once
                // the move was confirmed and launched (not on a Cancel'd overwrite prompt),
                // otherwise a cancelled paste would silently discard the cut. The move itself
                // completes in the background after this.
                await clipboard.ClearAsync();
            }
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
            process.StartInfo.FileName = "x-terminal-emulator";
            process.StartInfo.Arguments = $"-e {program} \"{argument}\"";
            process.StartInfo.UseShellExecute = false;
            process.Start();
        }

        public async Task CreateNewFolder(string name)
        {
            string newFolder = Path.Combine(CurrentDirectory, name);
            if (_fs.DirectoryExists(newFolder))
            {
                MessageBox_Show(null, Resources.FolderExists, Resources.Alert, ButtonEnum.Ok);
                return;
            }
            await _fs.CreateDirectoryAsync(newFolder);
        }

        public void ProcessCurrentItem(bool goToParent = false)
        {
            if (CurrentItem == null)
            {
                return;
            }

            if (goToParent)
            {
                var parentPath = _fs.GetDirectoryParent(CurrentDirectory);
                if (parentPath == null)
                {
                    return;
                }
                _pendingRestoreItemName = Path.GetFileName(CurrentDirectory);
                CurrentDirectory = parentPath;
                return;
            }

            if (CurrentItem.IsFolder)
            {
                if (CurrentItem.FullName == "..")
                {
                    var parentPath = _fs.GetDirectoryParent(CurrentDirectory);
                    _pendingRestoreItemName = Path.GetFileName(CurrentDirectory);
                    CurrentDirectory = parentPath ?? CurrentDirectory;
                }
                else
                {
                    CurrentDirectory = CurrentItem.FullName;
                }
            }
            else
            {
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

            if (!_fs.DirectoryExists(dir) || !Path.IsPathFullyQualified(dir))
            {
                return;
            }

            bool isParent = _fs.GetDirectoryParent(dir) != null;
            string? selectedDrive = OperatingSystem.IsWindows() ? _fs.GetPathRoot(dir) : null;

            var options = new EnumerationOptions
            {
                AttributesToSkip = OptionsModel.Instance.IsHiddenSystemFilesDisplayed
                    ? 0 : FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            IReadOnlyList<string> dirPaths;
            IReadOnlyList<string> filePaths;
            try
            {
                var dirsTask = _fs.GetDirectoriesAsync(dir, options, cts.Token);
                var filesTask = _fs.GetFilesAsync(dir, options, cts.Token);
                await Task.WhenAll(dirsTask, filesTask);
                dirPaths = dirsTask.Result;
                filePaths = filesTask.Result;
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

            // Build FileViewModel entries off the UI thread (CreateAsync does metadata I/O via service)
            List<FileViewModel> folders;
            List<FileViewModel> files;
            int totalFolders, totalFiles;
            try
            {
                (folders, files, totalFolders, totalFiles) =
                    await Task.Run(() => BuildEntries(dirPaths, filePaths, sortingBy, ascending, _fs, cts.Token), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to build entries for {Dir}", dir);
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
                _pendingScrollTargetFullName = null;
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

        private static async Task<(List<FileViewModel> Folders, List<FileViewModel> Files, int TotalFolders, int TotalFiles)>
            BuildEntries(IReadOnlyList<string> dirPaths, IReadOnlyList<string> filePaths,
                         SortingBy sorting, bool ascending, IFileSystemService fs, CancellationToken ct)
        {
            int totalFolders = 0, totalFiles = 0;
            var foldersList = new List<FileViewModel>();
            foreach (string subdirectory in dirPaths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    foldersList.Add(await FileViewModel.CreateAsync(subdirectory, true, fs));
                    ++totalFolders;
                }
                catch { }
            }

            var filesList = new List<FileViewModel>();
            foreach (string fileName in filePaths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    filesList.Add(await FileViewModel.CreateAsync(fileName, false, fs));
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

            return (foldersList, filesList, totalFolders, totalFiles);
        }

        public void NavigateToFileItem(string resultFilename)
        {
            var parent = Path.GetDirectoryName(resultFilename);
            if (parent == null)
            {
                return;
            }
            _pendingScrollTargetFullName = resultFilename;
            CurrentDirectory = parent;
        }

        string? _selectedDrive;
        string? SelectedDrive
        {
            get { return _selectedDrive; }
            set
            {
                if (value == null || !_fs.DirectoryExists(value))
                {
                    MessageBox_Show(null, Resources.DriveNotAvailable, Resources.Alert, ButtonEnum.Ok);
                    return;
                }

                var driveFromDirectory = _fs.GetPathRoot(CurrentDirectory);

                _selectedDrive = value;
                if (_selectedDrive != driveFromDirectory)
                {
                    CurrentDirectory = _selectedDrive!;
                }
                this.RaisePropertyChanged(nameof(SelectedDrive));
            }
        }
    }
}
