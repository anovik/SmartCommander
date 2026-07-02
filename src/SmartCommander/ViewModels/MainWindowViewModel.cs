using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Models;
using SmartCommander.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application = Avalonia.Application;

namespace SmartCommander.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fs;

        public MainWindowViewModel(IFileSystemService fs)
        {
            _fs = fs;

            ShowCopyDialog = new Interaction<CopyMoveViewModel, CopyMoveViewModel?>();
            ShowOptionsDialog = new Interaction<OptionsViewModel, OptionsViewModel?>();
            ShowSearchDialog = new Interaction<FileSearchViewModel, FileSearchViewModel?>();

            ExitCommand = ReactiveCommand.Create(Exit);
            SortNameCommand = ReactiveCommand.Create(SortName);
            SortExtensionCommand = ReactiveCommand.Create(SortExtension);
            SortSizeCommand = ReactiveCommand.Create(SortSize);
            SortDateCommand = ReactiveCommand.Create(SortDate);
            SearchFilesCommand = ReactiveCommand.CreateFromTask(SearchFilesDialog);

            EnterCommand = ReactiveCommand.Create(Execute);
            F3Command = ReactiveCommand.Create(View);
            F4Command = ReactiveCommand.Create(Edit);
            F5Command = ReactiveCommand.CreateFromTask(Copy);
            F6Command = ReactiveCommand.CreateFromTask(Move);
            F7Command = ReactiveCommand.Create(CreateNewFolder);
            F8Command = ReactiveCommand.Create(Delete);

            CopyToClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Copy());
            CutToClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Cut());
            PasteFromClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Paste());

            OptionsCommand = ReactiveCommand.CreateFromTask(ShowOptions);

            LeftFileViewModel = new FilesPaneViewModel(this, OnFocusChanged, _fs);
            RightFileViewModel = new FilesPaneViewModel(this, OnFocusChanged, _fs);
            SelectedPane = RightFileViewModel;

            if (!string.IsNullOrEmpty(OptionsModel.Instance.LeftPanePath))
            {
                LeftFileViewModel.CurrentDirectory = OptionsModel.Instance.LeftPanePath;
            }
            if (!string.IsNullOrEmpty(OptionsModel.Instance.RightPanePath))
            {
                RightFileViewModel.CurrentDirectory = OptionsModel.Instance.RightPanePath;
            }
            SetLanguage();
            SetTheme();
        }

        private void SetLanguage()
        {
            var cultureName = OptionsModel.Instance.Language;
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private void OnFocusChanged(object? sender, EventArgs e)
        {
            if (sender is FilesPaneViewModel)
            {
                SelectedPane = (FilesPaneViewModel)sender;
            }
        }

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        public ReactiveCommand<Unit, Unit> SortNameCommand { get; }
        public ReactiveCommand<Unit, Unit> SortExtensionCommand { get; }
        public ReactiveCommand<Unit, Unit> SortSizeCommand { get; }
        public ReactiveCommand<Unit, Unit> SortDateCommand { get; }
        public ReactiveCommand<Unit, Unit> SearchFilesCommand { get; }
        public ReactiveCommand<Unit, Unit> EnterCommand { get; }

        public ReactiveCommand<Unit, Unit> F3Command { get; }
        public ReactiveCommand<Unit, Unit> F4Command { get; }
        public ReactiveCommand<Unit, Unit> F5Command { get; }
        public ReactiveCommand<Unit, Unit> F6Command { get; }
        public ReactiveCommand<Unit, Unit> F7Command { get; }
        public ReactiveCommand<Unit, Unit> F8Command { get; }

        public ReactiveCommand<Unit, Unit> CopyToClipboardCommand { get; }
        public ReactiveCommand<Unit, Unit> CutToClipboardCommand { get; }
        public ReactiveCommand<Unit, Unit> PasteFromClipboardCommand { get; }

        public ReactiveCommand<Unit, Unit> OptionsCommand { get; }

        public FilesPaneViewModel LeftFileViewModel { get; }

        public FilesPaneViewModel RightFileViewModel { get; }

        private string _commandText = "";

        volatile bool _F3Busy;
        volatile bool _F4Busy;
        volatile bool _F7Busy;
        volatile bool _F8Busy;

        public string CommandText
        {
            get { return _commandText; }
            set
            {
                _commandText = value;
                this.RaisePropertyChanged(nameof(CommandText));
            }
        }

        public Interaction<CopyMoveViewModel, CopyMoveViewModel?> ShowCopyDialog { get; }

        public Interaction<OptionsViewModel, OptionsViewModel?> ShowOptionsDialog { get; }
        public Interaction<FileSearchViewModel, FileSearchViewModel?> ShowSearchDialog { get; }

        public static bool IsFunctionKeysDisplayed => OptionsModel.Instance.IsFunctionKeysDisplayed;
        public static bool IsCommandLineDisplayed => OptionsModel.Instance.IsCommandLineDisplayed;

        public void Exit()
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Shutdown();
            }
        }

        public void SortName()
        {
            SelectedPane.Sorting = SortingBy.SortingByName;
            SelectedPane.Ascending = true;
        }

        public void SortExtension()
        {
            SelectedPane.Sorting = SortingBy.SortingByExt;
            SelectedPane.Ascending = true;
        }

        public void SortSize()
        {
            SelectedPane.Sorting = SortingBy.SortingBySize;
            SelectedPane.Ascending = true;
        }

        public void SortDate()
        {
            SelectedPane.Sorting = SortingBy.SortingByDate;
            SelectedPane.Ascending = true;
        }

        public async Task SearchFilesDialog()
        {
            var searchModel = new FileSearchViewModel(SelectedPane.CurrentDirectory, _fs);
            await ShowSearchDialog.Handle(searchModel);
            searchModel.CancelSearch();

            if (searchModel.ResultFilename != string.Empty)
            {
                SelectedPane.NavigateToFileItem(searchModel.ResultFilename);
            }
        }

        public FilesPaneViewModel SecondPane
        {
            get
            {
                if (SelectedPane == RightFileViewModel)
                {
                    return LeftFileViewModel;
                }
                else
                {
                    return RightFileViewModel;
                }
            }
        }

        private FilesPaneViewModel _selectedPane = null!;
        public FilesPaneViewModel SelectedPane
        {
            get => _selectedPane;
            set
            {
                if (ReferenceEquals(_selectedPane, value)) { return; }
                var old = _selectedPane;
                this.RaiseAndSetIfChanged(ref _selectedPane, value);
                if (old != null) { old.IsSelected = false; }
                if (value != null) { value.IsSelected = true; }
            }
        }

        public void Execute()
        {
            SelectedPane.Execute(CommandText);
            CommandText = "";
        }

        public void View()
        {
            if (_F3Busy)
            {
                return;
            }
            _F3Busy = true;
            _ = SelectedPane.View(F3Finished);
        }

        public void Edit()
        {
            if (_F4Busy)
            {
                return;
            }
            _F4Busy = true;
            SelectedPane.Edit(F4Finished);
        }

        // Each long operation (Copy/Move/Paste/Delete/Zip/Unzip) runs as an independent
        // FileOperationViewModel with its own cancellation token and progress reporter.
        // This collection is mutated only on the UI thread: every operation is launched from
        // a command handler or dialog continuation, and Avalonia's synchronization context
        // resumes the awaits (including the finally in RunOperationAsync) on the UI thread.
        // The OperationsWindow show/hide handler and its ItemsControl binding rely on that.
        public ObservableCollection<FileOperationViewModel> ActiveOperations { get; } = new();

        public void CancelAllOperations()
        {
            foreach (var operation in ActiveOperations.ToList())
            {
                operation.Cancel();
            }
        }

        // The single funnel every long operation goes through. Never throws, so entry points
        // can launch it fire-and-forget after their dialog phase completes.
        private async Task RunOperationAsync(string description, string logContext,
            Func<IProgress<int>, CancellationToken, Task> work)
        {
            var operation = new FileOperationViewModel(description);
            ActiveOperations.Add(operation);
            try
            {
                await work(operation.ProgressReporter, operation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "{LogContext} failed", logContext);
            }
            finally
            {
                ActiveOperations.Remove(operation);
                operation.Dispose();
            }
        }

        private static string DescribeItems(int count, string firstItemName)
        {
            return count == 1 ? firstItemName : string.Format(Resources.ItemsNumber, count);
        }

        public async Task Zip()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            try
            {
                var pane = SelectedPane;
                var items = pane.CurrentItems.Select(i => (i.FullName, i.IsFolder, i.Name)).ToList();
                string zipDir = pane.CurrentDirectory;
                long totalSize = await _fs.GetTotalSizeAsync(items.Select(i => (i.FullName, i.IsFolder)).ToList());
                string description = string.Format(Resources.OperationZipDescription,
                    DescribeItems(items.Count, items[0].Name),
                    Path.Combine(zipDir, items[0].Name + ".zip"));
                _ = RunZipAsync();

                async Task RunZipAsync()
                {
                    await RunOperationAsync(description, "Zip",
                        (progress, ct) => Task.Run(() => ZipCore(items, zipDir, totalSize, progress, ct), ct));
                    pane.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Zip failed");
            }
        }

        public async Task Unzip()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            try
            {
                // Snapshot on the UI thread: the pane's selection and directory must not be
                // read from the Task.Run thread, and may change while the operation runs.
                var pane = SelectedPane;
                var archiveFullName = pane.CurrentItems[0].FullName;
                var archiveName = pane.CurrentItems[0].Name;
                var destDir = Path.Combine(pane.CurrentDirectory, archiveName);
                string description = string.Format(Resources.OperationUnzipDescription, archiveName, destDir);
                _ = RunUnzipAsync();

                async Task RunUnzipAsync()
                {
                    await RunOperationAsync(description, "Unzip",
                        (progress, ct) => Task.Run(() => UnzipCore(archiveFullName, destDir, progress, ct), ct));
                    pane.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unzip failed");
            }
        }

        private void UnzipCore(string archiveFullName, string destDir, IProgress<int> progress, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                if (Directory.Exists(destDir))
                {
                    MessageBox_Show(null, string.Format(Resources.DirectoryExists, destDir), Resources.Alert);
                    return;
                }
                progress.Report(0);
                ZipFile.ExtractToDirectory(archiveFullName, destDir);
                progress.Report(100);
            }
            catch { }
        }

        private void ZipCore(List<(string FullName, bool IsFolder, string Name)> snapshot, string zipDir, long totalSize, IProgress<int> progress, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                if (snapshot.Count < 1)
                {
                    return;
                }

                var zipName = Path.Combine(zipDir, snapshot[0].Name + ".zip");
                if (File.Exists(zipName))
                {
                    MessageBox_Show(null, string.Format(Resources.ArchiveExists, zipName), Resources.Alert);
                    return;
                }
                progress.Report(0);
                long processedSize = 0;

                List<Tuple<string, string>> itemsToProcess = new();
                foreach (var item in snapshot)
                {
                    itemsToProcess.Add(Tuple.Create("", item.FullName));
                }

                using (var zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
                {
                    while (itemsToProcess.Count > 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            ct.ThrowIfCancellationRequested();
                        }
                        var item = itemsToProcess[0];
                        var entryPath = item.Item1 as string;
                        var path = item.Item2 as string;
                        if (Directory.Exists(path))
                        {
                            var newEntryPath = Path.Combine(entryPath, new DirectoryInfo(path).Name);
                            foreach (var folder in Directory.GetDirectories(path))
                            {
                                itemsToProcess.Add(Tuple.Create(newEntryPath, folder));
                            }
                            foreach (var file in Directory.GetFiles(path))
                            {
                                itemsToProcess.Add(Tuple.Create(newEntryPath, file));
                            }
                        }
                        else if (File.Exists(path))
                        {
                            processedSize += new FileInfo(path).Length;
                            zip.CreateEntryFromFile(sourceFileName: path,
                                entryName: Path.Combine(item.Item1, Path.GetFileName(path)),
                                CompressionLevel.Optimal);
                        }

                        itemsToProcess.Remove(item);

                        progress.Report(totalSize == 0 ? 0 : (int)(processedSize * 100 / totalSize));
                    }
                }

                progress.Report(100);
            }
            catch { }
        }

        public async Task Copy()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }
            if (SelectedPane.CurrentDirectory == SecondPane.CurrentDirectory)
            {
                MessageBox_Show(null, Resources.CantCopyFileToItself, Resources.Alert);
                return;
            }
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
             string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            var copy = new CopyMoveViewModel(true, text, SecondPane.CurrentDirectory);
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null && result.IsConfirmed)
            {
                var items = SelectedPane.CurrentItems.Select(i => (i.FullName, i.IsFolder)).ToList();
                var destDirectory = SecondPane.CurrentDirectory;
                await ConfirmOverwriteThenRun(items, destDirectory,
                    overwrite => RunFileOperation(items, destDirectory, false, overwrite, "CopySelectedFiles"));
            }
        }

        public void F3Finished(ButtonResult result, object? parameter)
        {
            _F3Busy = false;
        }

        public void F4Finished(ButtonResult result, object? parameter)
        {
            _F4Busy = false;
        }

        public async Task Move()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }
            if (SelectedPane.CurrentDirectory == SecondPane.CurrentDirectory)
            {
                MessageBox_Show(null, Resources.CantMoveFileToItself, Resources.Alert);
                return;
            }
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
               string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            var copy = new CopyMoveViewModel(false, text, SecondPane.CurrentDirectory);
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null && result.IsConfirmed)
            {
                var items = SelectedPane.CurrentItems.Select(i => (i.FullName, i.IsFolder)).ToList();
                var destDirectory = SecondPane.CurrentDirectory;
                await ConfirmOverwriteThenRun(items, destDirectory,
                    overwrite => RunFileOperation(items, destDirectory, true, overwrite, "MoveSelectedItems"));
            }
        }

        public async Task<bool> PasteFiles(string destDirectory, List<string> sourcePaths, bool isCut)
        {
            var items = await Task.Run(() => sourcePaths
                .Select(p => (FullName: p, IsFolder: _fs.DirectoryExists(p)))
                .ToList());
            if (items.Count == 0)
            {
                return false;
            }

            return await ConfirmOverwriteThenRun(items, destDirectory,
                overwrite => RunFileOperation(items, destDirectory, isCut, overwrite, "PasteSelectedItems"));
        }

        // Returns true only once the user has actually confirmed (or no confirmation was needed)
        // and the file operation has been launched, so callers can tell a genuine launch apart
        // from a Cancel answer on the overwrite prompt. The operation itself completes in the
        // background after this returns.
        private async Task<bool> ConfirmOverwriteThenRun(List<(string FullName, bool IsFolder)> items, string destDirectory,
            Func<bool, Task> onConfirmed)
        {
            var duplicates = await _fs.GetDuplicatesAsync(items, destDirectory);
            bool overwrite = false;
            if (duplicates != null && duplicates.Count > 0)
            {
                var text = duplicates.Count == 1 ? Path.GetFileName(duplicates[0]) :
                    string.Format(Resources.ItemsNumber, duplicates.Count);
                var tcs = new TaskCompletionSource<ButtonResult>();
                MessageBox_Show((result, _) => tcs.TrySetResult(result),
                    string.Format(Resources.FileExistsRewrite, text), Resources.Alert, ButtonEnum.YesNoCancel);
                var result = await tcs.Task;
                if (result == ButtonResult.Cancel)
                {
                    return false;
                }
                overwrite = result == ButtonResult.Yes;
            }

            await onConfirmed(overwrite);
            return true;
        }

        private Task RunFileOperation(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, string logContext)
        {
            var sourcePane = SelectedPane;
            var destPane = SecondPane;
            string description = string.Format(
                move ? Resources.OperationMoveDescription : Resources.OperationCopyDescription,
                DescribeItems(items.Count, Path.GetFileName(items[0].FullName)),
                destDirectory);
            _ = RunAndRefreshAsync();
            return Task.CompletedTask;

            async Task RunAndRefreshAsync()
            {
                await RunOperationAsync(description, logContext,
                    (progress, ct) => CopyOrMoveItemsAsync(items, destDirectory, move, overwrite, progress, ct));
                sourcePane.Update();
                destPane.Update();
            }
        }

        private async Task CopyOrMoveItemsAsync(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, IProgress<int> progress, CancellationToken ct)
        {
            foreach (var (fullName, isFolder) in items)
            {
                if (IsSameDirectory(fullName, destDirectory))
                {
                    MessageBox_Show(null, move ? Resources.CantMoveFileToItself : Resources.CantCopyFileToItself, Resources.Alert);
                    return;
                }
                if (isFolder && IsDestinationInsideSource(fullName, destDirectory))
                {
                    MessageBox_Show(null, move ? Resources.CantMoveFolderToItself : Resources.CantCopyFolderToItself, Resources.Alert);
                    return;
                }
            }

            progress.Report(0);
            long totalSize = await _fs.GetTotalSizeAsync(items);
            long processedSize = 0;

            foreach (var (fullName, isFolder) in items)
            {
                ct.ThrowIfCancellationRequested();
                if (isFolder)
                {
                    try
                    {
                        string destFolder = Path.Combine(destDirectory, Path.GetFileName(fullName));
                        if (move)
                        {
                            bool sameDrive = string.Equals(
                                _fs.GetPathRoot(fullName),
                                _fs.GetPathRoot(destDirectory),
                                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                            if (sameDrive && !_fs.DirectoryExists(destFolder))
                            {
                                await _fs.MoveDirectoryAsync(fullName, destFolder);
                            }
                            else
                            {
                                processedSize = await _fs.CopyDirectoryAsync(
                                    fullName, destFolder, true, overwrite,
                                    progress, processedSize, totalSize, ct);
                                await _fs.DeleteDirectoryAsync(fullName, ct);
                            }
                        }
                        else
                        {
                            processedSize = await _fs.CopyDirectoryAsync(
                                fullName, destFolder, true, overwrite,
                                progress, processedSize, totalSize, ct);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        MessageBox_Show(null, move ? Resources.CantMoveFolderHere : Resources.CantCopyFolderHere, Resources.Alert);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        string destFile = Path.Combine(destDirectory, Path.GetFileName(fullName));
                        processedSize = await _fs.CopyFileAsync(
                            fullName, destFile, move, overwrite,
                            progress, processedSize, totalSize, ct);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        MessageBox_Show(null, move ? Resources.CantMoveFileHere : Resources.CantCopyFileHere, Resources.Alert);
                        return;
                    }
                }
            }
        }

        public async Task ShowOptions()
        {
            var optionsModel = new OptionsViewModel();
            var result = await ShowOptionsDialog.Handle(optionsModel);
            if (result != null)
            {
                this.RaisePropertyChanged(nameof(IsFunctionKeysDisplayed));
                this.RaisePropertyChanged(nameof(IsCommandLineDisplayed));
                SelectedPane.RaisePropertyChanged(nameof(FilesPaneViewModel.IsCurrentDirectoryDisplayed));
                SecondPane.RaisePropertyChanged(nameof(FilesPaneViewModel.IsCurrentDirectoryDisplayed));
                SetTheme();
            }
        }

        private void SetTheme()
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = OptionsModel.Instance.IsDarkThemeEnabled ?
                    ThemeVariant.Dark : ThemeVariant.Light;
            }
        }

        public void CreateNewFolder()
        {
            if (_F7Busy)
            {
                return;
            }
            _F7Busy = true;
            MessageBoxInput_Show(CreateNewFolderAnswer, Resources.CreateNewFolder);
        }

        public async void CreateNewFolderAnswer(string result)
        {
            try
            {
                if (!string.IsNullOrEmpty(result))
                {
                    await SelectedPane.CreateNewFolder(result);
                    SelectedPane.Update();
                    SecondPane.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CreateNewFolder failed");
                MessageBox_Show(null, Resources.CantCreateFolder, Resources.Alert);
            }
            finally
            {
                _F7Busy = false;
            }
        }

        public void Delete()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }
            // _F8Busy only guards the confirmation-dialog phase (no double dialogs on repeated
            // F8); the delete itself runs as a background operation and may overlap with others.
            if (_F8Busy)
            {
                return;
            }
            _F8Busy = true;
            var text = DescribeItems(SelectedPane.CurrentItems.Count, SelectedPane.CurrentItems[0].Name);
            MessageBox_Show(DeleteAnswer,
                string.Format(Resources.DeleteConfirmation, text),
                Resources.Alert,
                ButtonEnum.YesNo);
        }

        public async void DeleteAnswer(ButtonResult result, object? parameter)
        {
            if (result == ButtonResult.Yes)
            {
                try
                {
                    var items = SelectedPane.CurrentItems.Select(i => (i.FullName, i.IsFolder)).ToList();
                    var nonEmptyFolders = await _fs.GetNonEmptyFoldersAsync(items);
                    if (nonEmptyFolders != null && nonEmptyFolders.Count > 0)
                    {
                        var text = nonEmptyFolders.Count == 1 ? Path.GetFileName(nonEmptyFolders[0]) :
                          string.Format(Resources.ItemsNumber, nonEmptyFolders.Count);
                        MessageBox_Show(DeleteAnswerNonEmptyFolder,
                            string.Format(Resources.DeleteConfirmationNonEmpty, text),
                            Resources.Alert,
                            ButtonEnum.YesNoCancel,
                            parameter: (items, nonEmptyFolders));
                    }
                    else
                    {
                        DeleteSelectedItems(true, items, nonEmptyFolders);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Delete preparation failed");
                    _F8Busy = false;
                }
            }
            else
            {
                _F8Busy = false;
            }
        }

        public void DeleteAnswerNonEmptyFolder(ButtonResult result, object? parameter)
        {
            if (result != ButtonResult.Cancel)
            {
                var (items, nonEmptyFolders) =
                    ((List<(string FullName, bool IsFolder)>, List<string>))parameter!;
                DeleteSelectedItems(result == ButtonResult.Yes, items, nonEmptyFolders);
            }
            else
            {
                _F8Busy = false;
            }
        }

        private async void DeleteSelectedItems(bool overwrite,
            List<(string FullName, bool IsFolder)> items, List<string>? nonEmptyFolders)
        {
            // Dialog phase is over: allow the next F8 while this delete runs in the background.
            _F8Busy = false;
            try
            {
                var pane = SelectedPane;
                var secondPane = SecondPane;
                var itemsToDelete = items
                    .Where(item => overwrite || nonEmptyFolders == null || !nonEmptyFolders.Contains(item.FullName))
                    .ToList();
                if (itemsToDelete.Count == 0)
                {
                    return;
                }

                string description = string.Format(Resources.OperationDeleteDescription,
                    DescribeItems(itemsToDelete.Count, Path.GetFileName(itemsToDelete[0].FullName)));
                await RunOperationAsync(description, "DeleteSelectedItems",
                    (progress, ct) => DeleteItemsCoreAsync(itemsToDelete, progress, ct));
                pane.Update();
                secondPane.Update();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeleteSelectedItems failed");
            }
        }

        private async Task DeleteItemsCoreAsync(List<(string FullName, bool IsFolder)> items,
            IProgress<int> progress, CancellationToken ct)
        {
            progress.Report(0);
            int total = items.Count;
            int done = 0;

            foreach (var (fullName, isFolder) in items)
            {
                ct.ThrowIfCancellationRequested();
                if (isFolder)
                {
                    await _fs.DeleteDirectoryAsync(fullName, ct);
                }
                else
                {
                    await _fs.DeleteFileAsync(fullName);
                }
                done++;
                progress.Report(done * 100 / total);
            }
        }

        internal static bool IsDestinationInsideSource(string sourceFolder, string destination)
        {
            var src = sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dst = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(src, dst, comparison) ||
                   dst.StartsWith(src + Path.DirectorySeparatorChar, comparison);
        }

        internal static bool IsSameDirectory(string sourceFullName, string destDirectory)
        {
            var sourceDir = Path.GetDirectoryName(sourceFullName)?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
            var dst = destDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(sourceDir, dst, comparison);
        }

    }
}
