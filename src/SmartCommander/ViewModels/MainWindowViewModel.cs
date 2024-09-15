using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application = Avalonia.Application;
using File = System.IO.File;

namespace SmartCommander.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            ShowCopyDialog = new Interaction<CopyMoveViewModel, CopyMoveViewModel?>();
            ShowOptionsDialog = new Interaction<OptionsViewModel, OptionsViewModel?>();

            ExitCommand = ReactiveCommand.Create(Exit);
            SortNameCommand = ReactiveCommand.Create(SortName);
            SortExtensionCommand = ReactiveCommand.Create(SortExtension);
            SortSizeCommand = ReactiveCommand.Create(SortSize);
            SortDateCommand = ReactiveCommand.Create(SortDate);
            EnterCommand = ReactiveCommand.Create(Execute);
            F3Command = ReactiveCommand.Create(View);
            F4Command = ReactiveCommand.Create(Edit);
            F5Command = ReactiveCommand.CreateFromTask(Copy);
            F6Command = ReactiveCommand.CreateFromTask(Move);
            F7Command = ReactiveCommand.Create(CreateNewFolder);
            F8Command = ReactiveCommand.Create(Delete);
            OptionsCommand = ReactiveCommand.CreateFromTask(ShowOptions);

            LeftFileViewModel = new FilesPaneViewModel(this, OnFocusChanged);
            RightFileViewModel = new FilesPaneViewModel(this, OnFocusChanged);
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
            _progress = new Progress<int>(Progress_Show);
        }

        private static void SetLanguage()
        {
            string cultureName = OptionsModel.Instance.Language;
            CultureInfo culture = new(cultureName);
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
        public ReactiveCommand<Unit, Unit> EnterCommand { get; }

        public ReactiveCommand<Unit, Unit> F3Command { get; }
        public ReactiveCommand<Unit, Unit> F4Command { get; }
        public ReactiveCommand<Unit, Unit> F5Command { get; }
        public ReactiveCommand<Unit, Unit> F6Command { get; }
        public ReactiveCommand<Unit, Unit> F7Command { get; }
        public ReactiveCommand<Unit, Unit> F8Command { get; }
        public ReactiveCommand<Unit, Unit> OptionsCommand { get; }

        public FilesPaneViewModel LeftFileViewModel { get; }

        public FilesPaneViewModel RightFileViewModel { get; }

        private string _commandText = "";
        private readonly IProgress<int> _progress;
        private SmartCancellationTokenSource? tokenSource;
        private bool _F3Busy;
        private bool _F4Busy;
        private bool _F7Busy;
        private bool _F8Busy;

        public string CommandText
        {
            get => _commandText;
            set
            {
                _commandText = value;
                this.RaisePropertyChanged(nameof(CommandText));
            }
        }

        public Interaction<CopyMoveViewModel, CopyMoveViewModel?> ShowCopyDialog { get; }

        public Interaction<OptionsViewModel, OptionsViewModel?> ShowOptionsDialog { get; }

        public static bool IsFunctionKeysDisplayed
        {
            get
            {
                return OptionsModel.Instance.IsFunctionKeysDisplayed;
            }
        }

        public static bool IsCommandLineDisplayed
        {
            get
            {
                return OptionsModel.Instance.IsCommandLineDisplayed;
            }
        }

        public static void Exit()
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

        public FilesPaneViewModel SecondPane
        {
            get
            {
                return SelectedPane == RightFileViewModel ? LeftFileViewModel : RightFileViewModel;
            }
        }

        public FilesPaneViewModel SelectedPane { get; set; }

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

        public bool IsBackgroundOperation
        {
            get
            {
                return tokenSource != null && !tokenSource.IsDisposed;
            }
        }

        public void Cancel()
        {
            if (tokenSource != null && !tokenSource.IsDisposed)
            {
                tokenSource.Cancel();
            }
        }

        public async void Zip()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => ZipAsync(tokenSource.Token));
                SelectedPane.Update();
            }
        }

        public async void Unzip()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => UnzipAsync(tokenSource.Token));
                SelectedPane.Update();
            }

        }
        public void UnzipAsync(CancellationToken ct)
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

                string destDir = Path.Combine(SelectedPane.CurrentDirectory, SelectedPane.CurrentItems[0].Name);
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

        public void ZipAsync(CancellationToken ct)
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

                string zipName = Path.Combine(SelectedPane.CurrentDirectory, SelectedPane.CurrentItems[0].Name + ".zip");
                if (File.Exists(zipName))
                {
                    MessageBox_Show(null, string.Format(Resources.ArchiveExists, zipName), Resources.Alert);
                    return;
                }
                _progress?.Report(0);
                long totalSize = Utils.GetTotalSize(SelectedPane.CurrentItems);
                long processedSize = 0;

                List<FileViewModel> items = SelectedPane.CurrentItems;
                List<Tuple<string, string>> itemsToProcess = [];
                foreach (FileViewModel item in items)
                {
                    itemsToProcess.Add(Tuple.Create("", item.FullName));
                }

                using (ZipArchive zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
                {
                    while (itemsToProcess.Count > 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            ct.ThrowIfCancellationRequested();
                        }
                        Tuple<string, string> item = itemsToProcess[0];
                        string entryPath = item.Item1;
                        string path = item.Item2;
                        if (Directory.Exists(path))
                        {
                            string newEntryPath = Path.Combine(entryPath, new DirectoryInfo(path).Name);
                            foreach (string folder in Directory.GetDirectories(path))
                            {
                                itemsToProcess.Add(Tuple.Create(newEntryPath, folder));
                            }
                            foreach (string file in Directory.GetFiles(path))
                            {
                                itemsToProcess.Add(Tuple.Create(newEntryPath, file));
                            }
                        }
                        else if (File.Exists(path))
                        {
                            processedSize += Utils.GetTotalSize([new FileViewModel(path, false)]);
                            _ = zip.CreateEntryFromFile(sourceFileName: path,
                                entryName: Path.Combine(item.Item1, Path.GetFileName(path)),
                                CompressionLevel.Optimal);
                        }

                        _ = itemsToProcess.Remove(item);

                        Utils.ReportProgress(_progress, processedSize, totalSize);
                    }
                }

                _progress?.Report(100);
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
            string text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
             string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            CopyMoveViewModel copy = new(true, text, SecondPane.CurrentDirectory);
            CopyMoveViewModel result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {
                List<string> duplicates = Utils.GetDuplicates(SelectedPane.CurrentItems, SecondPane.CurrentDirectory);

                if (duplicates != null && duplicates.Count > 0)
                {
                    text = duplicates.Count == 1 ? Path.GetFileName(duplicates[0]) :
                     string.Format(Resources.ItemsNumber, duplicates.Count);
                    MessageBox_Show(CopyFileExists, string.Format(Resources.FileExistsRewrite, text),
                     Resources.Alert, ButtonEnum.YesNoCancel);
                }
                else
                {
                    CopySelectedFiles(false);
                }
            }
        }

        public void CopyFileExists(ButtonResult result, object? parameter)
        {
            if (result != ButtonResult.Cancel)
            {
                CopySelectedFiles(result == ButtonResult.Yes);
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

        private async void CopySelectedFiles(bool overwrite)
        {
            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => CopySelectedFilesAsync(overwrite, tokenSource.Token));

                SelectedPane.Update();
                SecondPane.Update();
            }
        }

        private void CopySelectedFilesAsync(bool overwrite, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                _progress?.Report(0);

                long totalSize = Utils.GetTotalSize(SelectedPane.CurrentItems);
                long processedSize = 0;

                foreach (FileViewModel item in SelectedPane.CurrentItems)
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (item.IsFolder)
                    {
                        try
                        {
                            string destFolder = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(item.FullName));
                            Utils.CopyDirectory(item.FullName, destFolder, recursive: true, overwrite, ct,
                                _progress, ref processedSize, totalSize);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            MessageBox_Show(null, Resources.CantMoveFolderHere, Resources.Alert);
                            return;
                        }
                    }
                    else
                    {
                        string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(item.FullName));
                        Utils.CopyFile(item.FullName, destFile, false, overwrite, ct,
                                _progress, ref processedSize, totalSize);
                    }
                    Utils.ReportProgress(_progress, processedSize, totalSize);
                }
                _progress?.Report(100);
            }
            catch { }
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
            string text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
               string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            CopyMoveViewModel copy = new(false, text, SecondPane.CurrentDirectory);
            CopyMoveViewModel result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {
                List<string> duplicates = Utils.GetDuplicates(SelectedPane.CurrentItems, SecondPane.CurrentDirectory);

                if (duplicates != null && duplicates.Count > 0)
                {
                    text = duplicates.Count == 1 ? Path.GetFileName(duplicates[0]) :
                        string.Format(Resources.ItemsNumber, duplicates.Count);
                    MessageBox_Show(MoveFileExists, string.Format(Resources.FileExistsRewrite, text),
                        Resources.Alert, ButtonEnum.YesNoCancel);
                }
                else
                {
                    MoveSelectedItems(false);
                }
            }
        }

        public void MoveFileExists(ButtonResult result, object? parameter)
        {
            if (result != ButtonResult.Cancel)
            {
                MoveSelectedItems(result == ButtonResult.Yes);
            }
        }

        private async void MoveSelectedItems(bool overwrite)
        {
            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => MoveSelectedItemsAsync(overwrite, tokenSource.Token));
                SelectedPane.Update();
                SecondPane.Update();
            }
        }

        private void MoveSelectedItemsAsync(bool overwrite, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                _progress?.Report(0);
                long totalSize = Utils.GetTotalSize(SelectedPane.CurrentItems);
                long processedSize = 0;
                foreach (FileViewModel item in SelectedPane.CurrentItems)
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (item.IsFolder)
                    {
                        try
                        {
                            // TODO: move this check to the top level
                            if (item.FullName == SecondPane.CurrentDirectory)
                            {
                                MessageBox_Show(null, Resources.CantMoveFolderToItself, Resources.Alert);
                                return;
                            }
                            string destFolder = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(item.FullName));
                            // TODO: in case of the same drive faster to move
                            Utils.CopyDirectory(item.FullName, destFolder, recursive: true, overwrite, ct,
                                _progress, ref processedSize, totalSize);
                            Utils.DeleteDirectoryWithHiddenFiles(item.FullName);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            MessageBox_Show(null, Resources.CantMoveFolderHere, Resources.Alert);
                            return;
                        }
                    }
                    else
                    {
                        string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(item.FullName));
                        Utils.CopyFile(item.FullName, destFile, true, overwrite, ct,
                                _progress, ref processedSize, totalSize);
                    }
                    Utils.ReportProgress(_progress, processedSize, totalSize);
                }
                _progress?.Report(100);
            }
            catch { }
        }

        public async Task ShowOptions()
        {
            OptionsViewModel optionsModel = new();
            OptionsViewModel result = await ShowOptionsDialog.Handle(optionsModel);
            if (result != null)
            {
                this.RaisePropertyChanged(nameof(IsFunctionKeysDisplayed));
                this.RaisePropertyChanged(nameof(IsCommandLineDisplayed));
                SelectedPane.RaisePropertyChanged("IsCurrentDirectoryDisplayed");
                SecondPane.RaisePropertyChanged("IsCurrentDirectoryDisplayed");
                SetTheme();
            }
        }

        private static void SetTheme()
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

        public void CreateNewFolderAnswer(string result)
        {
            if (!string.IsNullOrEmpty(result))
            {
                SelectedPane.CreateNewFolder(result);
                SelectedPane.Update();
                SecondPane.Update();
            }
            _F7Busy = false;
        }

        public void Delete()
        {
            if (SelectedPane.CurrentItems.Count < 1)
            {
                return;
            }

            if (_F8Busy)
            {
                return;
            }
            _F8Busy = true;
            string text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
                string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            MessageBox_Show(DeleteAnswer,
                string.Format(Resources.DeleteConfirmation, text),
                Resources.Alert,
                ButtonEnum.YesNo);
        }

        public void DeleteAnswer(ButtonResult result, object? parameter)
        {
            if (result == ButtonResult.Yes)
            {
                List<string>? nonEmptyFolders = Utils.GetNonEmptyFolders(SelectedPane.CurrentItems);
                if (nonEmptyFolders != null && nonEmptyFolders.Count > 0)
                {
                    string text = nonEmptyFolders.Count == 1 ? Path.GetFileName(nonEmptyFolders[0]) :
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
            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => DeleteSelectedItemsAsync(overwrite, nonEmptyFolders, tokenSource.Token));
                SelectedPane.Update();
                SecondPane.Update();
            }
            _F8Busy = false;
        }

        private void DeleteSelectedItemsAsync(bool overwrite, List<string>? nonEmptyFolders, CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                _progress?.Report(0);
                long totalSize = Utils.GetTotalSize(SelectedPane.CurrentItems);
                long processedSize = 0;
                foreach (FileViewModel item in SelectedPane.CurrentItems)
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    if (item == null)
                    {
                        continue;
                    }

                    if (!overwrite && nonEmptyFolders != null && nonEmptyFolders.Contains(item.FullName))
                    {
                        continue;
                    }
                    processedSize += Utils.GetTotalSize([item]);
                    FilesPaneViewModel.Delete(item);

                    Utils.ReportProgress(_progress, processedSize, totalSize);
                }
                _progress?.Report(100);

            }
            catch { }
        }
    }
}
