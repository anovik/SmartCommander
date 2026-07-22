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
using System.Collections.Specialized;
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
            ShowAboutDialog = new Interaction<AboutViewModel, AboutViewModel?>();

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
            F8Command = ReactiveCommand.CreateFromTask(Delete);

            CopyToClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Copy());
            CutToClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Cut());
            PasteFromClipboardCommand = ReactiveCommand.CreateFromTask(() => SelectedPane.Paste());

            OptionsCommand = ReactiveCommand.CreateFromTask(ShowOptions);
            AboutCommand = ReactiveCommand.CreateFromTask(ShowAbout);

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
        public ReactiveCommand<Unit, Unit> AboutCommand { get; }

        public FilesPaneViewModel LeftFileViewModel { get; }

        public FilesPaneViewModel RightFileViewModel { get; }

        private string _commandText = "";

        volatile bool _F3Busy;
        volatile bool _F4Busy;
        volatile bool _F7Busy;

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
        public Interaction<AboutViewModel, AboutViewModel?> ShowAboutDialog { get; }

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

        // One FileOperationViewModel per in-flight long operation (Copy/Move/Paste/Delete/Zip/Unzip).
        // Mutated only on the UI thread; OperationsWindow's show/hide and its ItemsControl binding rely on that.
        public ObservableCollection<FileOperationViewModel> ActiveOperations { get; } = new();

        // Waits for each cancelled operation's background cleanup (e.g. deleting a partial
        // destination file) to finish, not just for the cancellation request to be sent.
        public Task CancelAllOperationsAndWaitAsync()
        {
            if (ActiveOperations.Count == 0)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            void OnActiveOperationsChanged(object? s, NotifyCollectionChangedEventArgs e)
            {
                if (ActiveOperations.Count == 0)
                {
                    tcs.TrySetResult();
                }
            }
            ActiveOperations.CollectionChanged += OnActiveOperationsChanged;

            foreach (var operation in ActiveOperations.ToList())
            {
                operation.Cancel();
            }

            return WaitAndUnsubscribeAsync();

            async Task WaitAndUnsubscribeAsync()
            {
                try
                {
                    await tcs.Task;
                }
                finally
                {
                    ActiveOperations.CollectionChanged -= OnActiveOperationsChanged;
                }
            }
        }

        // The single funnel every long operation goes through. Never throws, so entry points
        // can launch it fire-and-forget after their dialog phase completes. Returns whether the
        // work completed without being cancelled or throwing.
        private async Task<bool> RunOperationAsync(string description, string logContext,
            Func<IProgress<int>, CancellationToken, Task> work)
        {
            var operation = new FileOperationViewModel(description);
            ActiveOperations.Add(operation);
            try
            {
                await work(operation.ProgressReporter, operation.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{LogContext} failed", logContext);
                return false;
            }
            finally
            {
                ActiveOperations.Remove(operation);
                operation.Dispose();
            }
        }

        // Wraps RunOperationAsync with the pane refresh that must follow every operation.
        // The refresh itself is guarded so a failure there (e.g. a pane's directory disappeared)
        // is logged instead of becoming an unobserved task exception.
        private async Task<bool> RunOperationAndRefreshAsync(string description, string logContext,
            Func<IProgress<int>, CancellationToken, Task> work, params FilesPaneViewModel[] panesToRefresh)
        {
            bool succeeded = await RunOperationAsync(description, logContext, work);
            foreach (var pane in panesToRefresh)
            {
                try
                {
                    pane.Update();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{LogContext} pane refresh failed", logContext);
                }
            }
            return succeeded;
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
                var zipName = Path.Combine(zipDir, items[0].Name + ".zip");
                // Checked here, before RunOperationAsync ever adds anything to ActiveOperations,
                // so an already-exists reject is silent and instant instead of flashing
                // OperationsWindow open then immediately closed.
                if (File.Exists(zipName))
                {
                    MessageBox_Show(null, string.Format(Resources.ArchiveExists, zipName), Resources.Alert);
                    return;
                }

                long totalSize = await _fs.GetTotalSizeAsync(items.Select(i => (i.FullName, i.IsFolder)).ToList());
                string description = string.Format(Resources.OperationZipDescription,
                    DescribeItems(items.Count, items[0].Name), zipName);
                _ = RunZipAsync();

                async Task RunZipAsync()
                {
                    await RunOperationAndRefreshAsync(description, "Zip",
                        (progress, ct) => Task.Run(() => ZipCore(items, zipName, totalSize, progress, ct), ct),
                        pane);
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
                // Checked here, before RunOperationAsync ever adds anything to ActiveOperations,
                // so an already-exists reject is silent and instant instead of flashing
                // OperationsWindow open then immediately closed.
                if (Directory.Exists(destDir))
                {
                    MessageBox_Show(null, string.Format(Resources.DirectoryExists, destDir), Resources.Alert);
                    return;
                }

                string description = string.Format(Resources.OperationUnzipDescription, archiveName, destDir);
                _ = RunUnzipAsync();

                async Task RunUnzipAsync()
                {
                    await RunOperationAndRefreshAsync(description, "Unzip",
                        (progress, ct) => Task.Run(() => UnzipCore(archiveFullName, destDir, progress, ct), ct),
                        pane);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unzip failed");
            }
        }

        // Extracted entry-by-entry (instead of one ZipFile.ExtractToDirectory call) so
        // cancellation takes effect between entries; ExtractToDirectory itself is not
        // cancellable mid-call.
        private void UnzipCore(string archiveFullName, string destDir, IProgress<int> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                progress.Report(0);
                Directory.CreateDirectory(destDir);
                string destDirFull = Path.GetFullPath(destDir + Path.DirectorySeparatorChar);

                using var archive = ZipFile.OpenRead(archiveFullName);
                var entries = archive.Entries;
                int total = entries.Count;
                int done = 0;
                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    string destPath = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
                    if (!destPath.StartsWith(destDirFull, StringComparison.Ordinal))
                    {
                        throw new IOException($"Zip entry is outside the target directory: {entry.FullName}");
                    }

                    if (entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }

                    done++;
                    progress.Report(total == 0 ? 100 : done * 100 / total);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log.Error(ex, "Unzip failed: {ArchiveFullName}", archiveFullName);
                MessageBox_Show(null, string.Format(Resources.CantExtractArchive, archiveFullName), Resources.Alert);
            }
        }

        private void ZipCore(List<(string FullName, bool IsFolder, string Name)> snapshot, string zipName, long totalSize, IProgress<int> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (snapshot.Count < 1)
            {
                return;
            }

            try
            {
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
                        ct.ThrowIfCancellationRequested();
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
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log.Error(ex, "Zip failed: {ZipName}", zipName);
                MessageBox_Show(null, string.Format(Resources.CantCreateArchive, zipName), Resources.Alert);
            }
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

        // onMoveCompleted (if given) runs only once the background move has actually finished
        // successfully - not merely been launched - so a cut-paste caller can safely clear its
        // clipboard without losing the source on a failed move.
        public async Task<bool> PasteFiles(string destDirectory, List<string> sourcePaths, bool isCut,
            Func<Task>? onMoveCompleted = null)
        {
            var items = await Task.Run(() => sourcePaths
                .Select(p => (FullName: p, IsFolder: _fs.DirectoryExists(p)))
                .ToList());
            if (items.Count == 0)
            {
                return false;
            }

            return await ConfirmOverwriteThenRun(items, destDirectory,
                overwrite => RunFileOperation(items, destDirectory, isCut, overwrite, "PasteSelectedItems",
                    onCompleted: onMoveCompleted == null ? null : async succeeded =>
                    {
                        if (succeeded)
                        {
                            await onMoveCompleted();
                        }
                    }));
        }

        // Returns true only once the user has confirmed (or no confirmation was needed) and the
        // operation has been launched - not completed; it keeps running in the background.
        private async Task<bool> ConfirmOverwriteThenRun(List<(string FullName, bool IsFolder)> items, string destDirectory,
            Func<bool, Task<bool>> onConfirmed)
        {
            var duplicates = await _fs.GetDuplicatesAsync(items, destDirectory);
            bool overwrite = false;
            if (duplicates != null && duplicates.Count > 0)
            {
                var text = duplicates.Count == 1 ? Path.GetFileName(duplicates[0]) :
                    string.Format(Resources.ItemsNumber, duplicates.Count);
                var result = await ShowMessageBoxAsync(
                    string.Format(Resources.FileExistsRewrite, text), ButtonEnum.YesNoCancel, ButtonResult.Cancel);
                if (result == ButtonResult.Cancel)
                {
                    return false;
                }
                overwrite = result == ButtonResult.Yes;
            }

            return await onConfirmed(overwrite);
        }

        // Wraps the callback-based MessageBox_Show in a Task so dialog continuations can be
        // awaited inline instead of chained through separate named callback methods.
        // defaultButton lets callers make the safer option (Cancel/No) the one Enter triggers.
        private Task<ButtonResult> ShowMessageBoxAsync(string text, ButtonEnum buttons, ButtonResult? defaultButton = null)
        {
            var tcs = new TaskCompletionSource<ButtonResult>();
            MessageBox_Show((result, _) => tcs.TrySetResult(result), text, Resources.Alert, buttons, defaultButton: defaultButton);
            return tcs.Task;
        }

        // Validated up front, before RunOperationAsync ever adds anything to ActiveOperations:
        // a same-directory/folder-into-itself failure must never make OperationsWindow flash
        // open then immediately closed for what should be a silent, instant no-op.
        private static string? ValidateItemsForOperation(List<(string FullName, bool IsFolder)> items,
            string destDirectory, bool move)
        {
            foreach (var (fullName, isFolder) in items)
            {
                if (IsSameDirectory(fullName, destDirectory))
                {
                    return move ? Resources.CantMoveFileToItself : Resources.CantCopyFileToItself;
                }
                if (isFolder && IsDestinationInsideSource(fullName, destDirectory))
                {
                    return move ? Resources.CantMoveFolderToItself : Resources.CantCopyFolderToItself;
                }
            }
            return null;
        }

        private Task<bool> RunFileOperation(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, string logContext, Func<bool, Task>? onCompleted = null)
        {
            var validationError = ValidateItemsForOperation(items, destDirectory, move);
            if (validationError != null)
            {
                MessageBox_Show(null, validationError, Resources.Alert);
                return Task.FromResult(false);
            }

            var sourcePane = SelectedPane;
            var destPane = SecondPane;
            string description = string.Format(
                move ? Resources.OperationMoveDescription : Resources.OperationCopyDescription,
                DescribeItems(items.Count, Path.GetFileName(items[0].FullName)),
                destDirectory);
            _ = RunAndRefreshAsync();
            return Task.FromResult(true);

            async Task RunAndRefreshAsync()
            {
                bool succeeded = await RunOperationAndRefreshAsync(description, logContext,
                    (progress, ct) => CopyOrMoveItemsAsync(items, destDirectory, move, overwrite, progress, ct),
                    sourcePane, destPane);
                if (onCompleted != null)
                {
                    try
                    {
                        await onCompleted(succeeded);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{LogContext} completion callback failed", logContext);
                    }
                }
            }
        }

        private async Task CopyOrMoveItemsAsync(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, IProgress<int> progress, CancellationToken ct)
        {
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
                                    fullName, destFolder, true, delete: true, overwrite,
                                    progress, processedSize, totalSize, ct);
                            }
                        }
                        else
                        {
                            processedSize = await _fs.CopyDirectoryAsync(
                                fullName, destFolder, true, delete: false, overwrite,
                                progress, processedSize, totalSize, ct);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        MessageBox_Show(null, move ? Resources.CantMoveFolderHere : Resources.CantCopyFolderHere, Resources.Alert);
                        throw new IOException($"Can't {(move ? "move" : "copy")} folder {fullName}", ex);
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
                    catch (Exception ex)
                    {
                        MessageBox_Show(null, move ? Resources.CantMoveFileHere : Resources.CantCopyFileHere, Resources.Alert);
                        throw new IOException($"Can't {(move ? "move" : "copy")} file {fullName}", ex);
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

        public async Task ShowAbout()
        {
            await ShowAboutDialog.Handle(new AboutViewModel());
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

        // Mirrors Copy/Move/Paste/Zip/Unzip: await only the confirmation dialog, then launch
        // the delete fire-and-forget so a second F8 on a different selection can run concurrently.
        public async Task Delete()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            var items = SelectedPane.CurrentItems.Select(i => (i.FullName, i.IsFolder)).ToList();
            var text = DescribeItems(items.Count, Path.GetFileName(items[0].FullName));
            var confirmResult = await ShowMessageBoxAsync(
                string.Format(Resources.DeleteConfirmation, text), ButtonEnum.YesNo, ButtonResult.No);
            if (confirmResult != ButtonResult.Yes)
            {
                return;
            }

            List<string>? nonEmptyFolders;
            try
            {
                nonEmptyFolders = await _fs.GetNonEmptyFoldersAsync(items);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Delete preparation failed");
                return;
            }

            bool overwrite = true;
            if (nonEmptyFolders != null && nonEmptyFolders.Count > 0)
            {
                var nonEmptyText = nonEmptyFolders.Count == 1 ? Path.GetFileName(nonEmptyFolders[0]) :
                    string.Format(Resources.ItemsNumber, nonEmptyFolders.Count);
                var nonEmptyResult = await ShowMessageBoxAsync(
                    string.Format(Resources.DeleteConfirmationNonEmpty, nonEmptyText), ButtonEnum.YesNoCancel, ButtonResult.Cancel);
                if (nonEmptyResult == ButtonResult.Cancel)
                {
                    return;
                }
                overwrite = nonEmptyResult == ButtonResult.Yes;
            }

            DeleteSelectedItems(overwrite, items, nonEmptyFolders);
        }

        private void DeleteSelectedItems(bool overwrite,
            List<(string FullName, bool IsFolder)> items, List<string>? nonEmptyFolders)
        {
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
                _ = RunOperationAndRefreshAsync(description, "DeleteSelectedItems",
                    (progress, ct) => DeleteItemsCoreAsync(itemsToDelete, progress, ct),
                    pane, secondPane);
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
