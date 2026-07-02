using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Threading;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Models;
using SmartCommander.Services;
using System;
using System.Collections.Generic;
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
            _progress = new FilteringProgress(Progress_Show);
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

        IProgress<int> _progress;

        SmartCancellationTokenSource? tokenSource;

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

        // Copy/Move/Zip/Unzip/Delete/PasteFiles all share this single tokenSource, and the progress
        // window is non-modal, so a second operation started while one is running would reassign
        // tokenSource out from under the first (see code_review.md). Checking IsBackgroundOperation
        // at the top of each of those entry points below is a deliberate interim guard matching
        // today's single-progress-window UI, not the long-term design: issue #77 (Multiple
        // simultaneous copy/move operations) wants these to run concurrently with independent
        // progress tracking. When #77 is implemented, remove these guards rather than reworking them.
        public bool IsBackgroundOperation => tokenSource != null && !tokenSource.IsDisposed;

        public void Cancel()
        {
            if (tokenSource != null && !tokenSource.IsDisposed)
            {
                tokenSource.Cancel();
            }
        }

        public async void Zip()
        {
            if (IsBackgroundOperation)
            {
                return;
            }
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            try
            {
                var items = SelectedPane.CurrentItems.Select(i => (i.FullName, i.IsFolder, i.Name)).ToList();
                string zipDir = SelectedPane.CurrentDirectory;
                long totalSize = await _fs.GetTotalSizeAsync(items.Select(i => (i.FullName, i.IsFolder)).ToList());
                using (tokenSource = new SmartCancellationTokenSource())
                {
                    await Task.Run(() => ZipCore(items, zipDir, totalSize, tokenSource.Token));
                    SelectedPane.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Zip failed");
            }
        }

        public async void Unzip()
        {
            if (IsBackgroundOperation)
            {
                return;
            }
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            try
            {
                using (tokenSource = new SmartCancellationTokenSource())
                {
                    await Task.Run(() => UnzipCore(tokenSource.Token));
                    SelectedPane.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unzip failed");
            }
        }

        private void UnzipCore(CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                if (SelectedPane.CurrentItems.Count < 1)
                {
                    return;
                }
                var destDir = Path.Combine(SelectedPane.CurrentDirectory, SelectedPane.CurrentItems[0].Name);
                if (Directory.Exists(destDir))
                {
                    MessageBox_Show(null, string.Format(Resources.DirectoryExists, destDir), Resources.Alert);
                    return;
                }
                _progress?.Report(0);
                ZipFile.ExtractToDirectory(SelectedPane.CurrentItems[0].FullName, destDir);
                _progress?.Report(100);
            }
            catch { }
        }

        private void ZipCore(List<(string FullName, bool IsFolder, string Name)> snapshot, string zipDir, long totalSize, CancellationToken ct)
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
                _progress?.Report(0);
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

                        _progress?.Report(totalSize == 0 ? 0 : (int)(processedSize * 100 / totalSize));
                    }
                }

                _progress?.Report(100);
            }
            catch { }
        }

        public async Task Copy()
        {
            if (IsBackgroundOperation)
            {
                return;
            }
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
            if (IsBackgroundOperation)
            {
                return;
            }
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
            if (IsBackgroundOperation)
            {
                return false;
            }

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
        // and the file operation has run to completion, so callers can tell a genuine run apart
        // from a Cancel answer instead of assuming the operation happened as soon as this returns.
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

        private async Task RunFileOperation(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, string logContext)
        {
            try
            {
                using (tokenSource = new SmartCancellationTokenSource())
                {
                    await CopyOrMoveItemsAsync(items, destDirectory, move, overwrite, tokenSource.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "{LogContext} failed", logContext);
            }
            finally
            {
                _progress?.Report(100);
                SelectedPane.Update();
                SecondPane.Update();
            }
        }

        private async Task CopyOrMoveItemsAsync(List<(string FullName, bool IsFolder)> items, string destDirectory,
            bool move, bool overwrite, CancellationToken ct)
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

            _progress?.Report(0);
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
                                    _progress, processedSize, totalSize, ct);
                                await _fs.DeleteDirectoryAsync(fullName, ct);
                            }
                        }
                        else
                        {
                            processedSize = await _fs.CopyDirectoryAsync(
                                fullName, destFolder, true, overwrite,
                                _progress, processedSize, totalSize, ct);
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
                            _progress, processedSize, totalSize, ct);
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
            if (IsBackgroundOperation)
            {
                return;
            }
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }
            if (_F8Busy)
            {
                return;
            }
            _F8Busy = true;
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
                string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
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
                            parameter: nonEmptyFolders);
                    }
                    else
                    {
                        DeleteSelectedItems(true, nonEmptyFolders);
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
                DeleteSelectedItems(result == ButtonResult.Yes, parameter as List<string>);
            }
            else
            {
                _F8Busy = false;
            }
        }

        private async void DeleteSelectedItems(bool overwrite, List<string>? nonEmptyFolders)
        {
            try
            {
                using (tokenSource = new SmartCancellationTokenSource())
                {
                    _progress?.Report(0);

                    var itemsToDelete = SelectedPane.CurrentItems
                        .Where(item => item != null &&
                                       (overwrite || nonEmptyFolders == null || !nonEmptyFolders.Contains(item.FullName)))
                        .ToList();

                    int total = itemsToDelete.Count;
                    int done = 0;

                    foreach (var item in itemsToDelete)
                    {
                        tokenSource.Token.ThrowIfCancellationRequested();
                        if (item.IsFolder)
                        {
                            await _fs.DeleteDirectoryAsync(item.FullName, tokenSource.Token);
                        }
                        else
                        {
                            await _fs.DeleteFileAsync(item.FullName);
                        }
                        done++;
                        _progress?.Report(total > 0 ? done * 100 / total : 100);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "DeleteSelectedItems failed");
            }
            finally
            {
                _progress?.Report(100);
                SelectedPane.Update();
                SecondPane.Update();
                _F8Busy = false;
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

        private sealed class FilteringProgress : IProgress<int>
        {
            private volatile int _last = -1;
            private readonly Action<int> _callback;
            internal FilteringProgress(Action<int> callback) => _callback = callback;
            public void Report(int value)
            {
                if (value == _last) return;
                _last = value;
                Dispatcher.UIThread.Post(() => _callback(value));
            }
        }
    }
}
