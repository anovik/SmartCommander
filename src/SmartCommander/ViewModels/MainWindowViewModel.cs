using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
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
            TabCommand = ReactiveCommand.Create(ChangeSelectedPane);
            OptionsCommand = ReactiveCommand.CreateFromTask(ShowOptions);

            LeftFileViewModel = new FilesPaneViewModel(this) { IsSelected = true };
            RightFileViewModel = new FilesPaneViewModel(this);

            if (!string.IsNullOrEmpty(OptionsModel.Instance.LeftPanePath))
            {
                LeftFileViewModel.CurrentDirectory = OptionsModel.Instance.LeftPanePath;
            }
            if (!string.IsNullOrEmpty(OptionsModel.Instance.RightPanePath))
            {
                RightFileViewModel.CurrentDirectory = OptionsModel.Instance.RightPanePath;
            }
            SetTheme();
            _progress = new Progress<int>(v => Progress_Show(v));
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
        public ReactiveCommand<Unit, Unit> TabCommand { get; }
        public ReactiveCommand<Unit, Unit> OptionsCommand { get; }

        public FilesPaneViewModel LeftFileViewModel { get; }

        public FilesPaneViewModel RightFileViewModel { get; }

        private string _commandText = "";

        IProgress<int> _progress;

        SmartCancellationTokenSource? tokenSource;

        public string CommandText
        {
            get { return _commandText; }
            set
            {
                _commandText = value;
                this.RaisePropertyChanged("CommandText");
            }
        }

        public Interaction<CopyMoveViewModel, CopyMoveViewModel?> ShowCopyDialog { get; }

        public Interaction<OptionsViewModel, OptionsViewModel?> ShowOptionsDialog { get; }

        public bool IsFunctionKeysDisplayed => OptionsModel.Instance.IsFunctionKeysDisplayed;
        public bool IsCommandLineDisplayed => OptionsModel.Instance.IsCommandLineDisplayed;

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

        public void ChangeSelectedPane()
        {
            if (LeftFileViewModel.IsSelected)
            {
                SelectedPane = RightFileViewModel;
            }
            else if (RightFileViewModel.IsSelected)
            {
                SelectedPane = LeftFileViewModel;
            }
        }

        public FilesPaneViewModel SecondPane
        {
            get
            {
                if (LeftFileViewModel.IsSelected)
                {
                    return RightFileViewModel;
                }
                else if (RightFileViewModel.IsSelected)
                {
                    return LeftFileViewModel;
                }
                throw new Exception(Resources.ErrorNoPane);
            }
        }

        public FilesPaneViewModel SelectedPane
        {
            get
            {
                if (LeftFileViewModel.IsSelected)
                {
                    return LeftFileViewModel;
                }
                else if (RightFileViewModel.IsSelected)
                {
                    return RightFileViewModel;
                }
                throw new Exception(Resources.ErrorNoPane);
            }
            set
            {
                if (RightFileViewModel == value && !RightFileViewModel.IsSelected)
                {
                    LeftFileViewModel.IsSelected = false;
                    RightFileViewModel.IsSelected = true;
                }
                else if (LeftFileViewModel == value && !LeftFileViewModel.IsSelected)
                {
                    RightFileViewModel.IsSelected = false;
                    LeftFileViewModel.IsSelected = true;
                }
            }
        }

        public void Execute()
        {
            SelectedPane.Execute(CommandText);
            CommandText = "";
        }

        public void View()
        {
            SelectedPane.View();
        }

        public void Edit()
        {
            SelectedPane.Edit();
        }

        public bool IsBackgroundOperation { get { return tokenSource != null && !tokenSource.IsDisposed; } }

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
                return;

            using (tokenSource = new SmartCancellationTokenSource())
            {
                await Task.Run(() => ZipAsync(tokenSource.Token));
                SelectedPane.Update();
            }
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
                    return;

                var zipName = Path.Combine(SelectedPane.CurrentDirectory, SelectedPane.CurrentItems[0].Name + ".zip");
                if (File.Exists(zipName))
                {
                    MessageBox_Show(null, string.Format(Resources.ArchiveExists, zipName), Resources.Alert);
                    return;
                }
                _progress?.Report(0);
                long counter = 0;

                var items = SelectedPane.CurrentItems;
                List<Tuple<string, string>> itemsToProcess = new();
                foreach (var item in items)
                {
                    itemsToProcess.Add(Tuple.Create("", item.FullName));
                }

                long totalItemsCount = itemsToProcess.Count;

                using (var zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
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
                                totalItemsCount++;
                            }
                            foreach (var file in Directory.GetFiles(path))
                            {
                                itemsToProcess.Add(Tuple.Create(newEntryPath, file));
                                totalItemsCount++;
                            }
                        }
                        else if (File.Exists(path))
                        {
                            zip.CreateEntryFromFile(sourceFileName: path,
                                entryName: Path.Combine(item.Item1, Path.GetFileName(path)),
                                CompressionLevel.Optimal);
                        }

                        itemsToProcess.Remove(item);

                        _progress?.Report(Convert.ToInt32(counter++ * 100 / totalItemsCount));
                    }

                _progress?.Report(100);
            }
            catch { }
        }
        public async Task Copy()
        {            
            if (SelectedPane.CurrentItems.Count < 1)
                return;
            if (SelectedPane.CurrentDirectory == SecondPane.CurrentDirectory)
            {
                MessageBox_Show(null, Resources.CantCopyFileToItself, Resources.Alert);
                return;
            }
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
             string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            var copy = new CopyMoveViewModel(true, text, SecondPane.CurrentDirectory);            
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {    
                var duplicates = Utils.GetDuplicates(SelectedPane.CurrentItems, SecondPane.CurrentDirectory);

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
                int counter = 0;
                foreach (var item in SelectedPane.CurrentItems)
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
                            Utils.CopyDirectory(item.FullName, destFolder, true, ct);
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
                        File.Copy(item.FullName, destFile, overwrite);

                    }
                    _progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
                }
                _progress?.Report(100);
            }
            catch { }
        }

        public async Task Move()
        {
            if (SelectedPane.CurrentItems.Count < 1)
                return;
            if (SelectedPane.CurrentDirectory == SecondPane.CurrentDirectory)
            {
                MessageBox_Show(null, Resources.CantMoveFileToItself, Resources.Alert);
                return;
            }
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
               string.Format(Resources.ItemsNumber, SelectedPane.CurrentItems.Count);
            var copy = new CopyMoveViewModel(false, text, SecondPane.CurrentDirectory);
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {        
                var duplicates = Utils.GetDuplicates(SelectedPane.CurrentItems, SecondPane.CurrentDirectory);

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

        private void MoveSelectedItemsAsync(bool overwrite,CancellationToken ct)
        {
            try 
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                _progress?.Report(0);
                int counter = 0;
                foreach (var item in SelectedPane.CurrentItems)
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
                            Utils.CopyDirectory(item.FullName, destFolder, true, ct);
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
                        File.Move(item.FullName, destFile, overwrite);
                    }
                    _progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
                }
                _progress?.Report(100);
            }
            catch { }
        }

        public async Task ShowOptions()
        {
            var optionsModel = new OptionsViewModel();
            var result = await ShowOptionsDialog.Handle(optionsModel);
            if (result != null)
            {
                this.RaisePropertyChanged("IsFunctionKeysDisplayed");
                this.RaisePropertyChanged("IsCommandLineDisplayed");
                SelectedPane.RaisePropertyChanged("IsCurrentDirectoryDisplayed");
                SecondPane.RaisePropertyChanged("IsCurrentDirectoryDisplayed");
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
        }

        public void Delete()
        {           
            if (SelectedPane.CurrentItems.Count < 1)
                return;
            var text = SelectedPane.CurrentItems.Count == 1 ? SelectedPane.CurrentItems[0].Name :
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
                var nonEmptyFolders = Utils.GetNonEmptyFolders(SelectedPane.CurrentItems);
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
        }

        public void DeleteAnswerNonEmptyFolder(ButtonResult result, object? parameter)
        {
            if (result != ButtonResult.Cancel)
            {
                DeleteSelectedItems(result == ButtonResult.Yes, parameter as List<string>);
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
                int counter = 0;
                foreach (var item in SelectedPane.CurrentItems)
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
                    SelectedPane.Delete(item);

                    _progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
                }
                _progress?.Report(100);
            }
            catch { }
        }     
    }
}
