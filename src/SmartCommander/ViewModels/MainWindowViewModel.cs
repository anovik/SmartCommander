using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Threading;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Application = Avalonia.Application;

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
            progress = new Progress<int>(v => Progress_Show(v));
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

        IProgress<int> progress;
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
            await Task.Run(() => CopySelectedFilesAsync(overwrite));

            SelectedPane.Update();
            SecondPane.Update();
        }

        private void CopySelectedFilesAsync(bool overwrite)
        {
            progress?.Report(0);
            int counter = 0;
            foreach (var item in SelectedPane.CurrentItems)
            {
                if (item.IsFolder)
                {                
                    try
                    {
                        string destFolder = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(item.FullName));
                        Utils.CopyDirectory(item.FullName, destFolder, true);
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
                progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
            }
            progress?.Report(100);
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
            await Task.Run(() => MoveSelectedItemsAsync(overwrite));
            SelectedPane.Update();
            SecondPane.Update();
        }

        private void MoveSelectedItemsAsync(bool overwrite)
        {
            progress?.Report(0);
            int counter = 0;
            foreach (var item in SelectedPane.CurrentItems)
            {
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
                        Utils.CopyDirectory(item.FullName, destFolder, true);
                        Utils.DeleteDirectoryWithHiddenFiles(item.FullName);                    
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
                progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
            }
            progress?.Report(100);
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
            await Task.Run(() => DeleteSelectedItemsAsync(overwrite, nonEmptyFolders));
            SelectedPane.Update();
            SecondPane.Update();
        }

        private void DeleteSelectedItemsAsync(bool overwrite, List<string>? nonEmptyFolders)
        {
            progress?.Report(0);
            int counter = 0;
            foreach (var item in SelectedPane.CurrentItems)
            {
                if (item == null)
                {
                    continue;
                }               

                if (!overwrite && nonEmptyFolders != null && nonEmptyFolders.Contains(item.FullName))
                {
                    continue;
                }
                SelectedPane.Delete(item);

                progress?.Report(++counter * 100 / SelectedPane.CurrentItems.Count);
            }
            progress?.Report(100);
        }     
    }
}
