using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Models;
using System;
using System.Data;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

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
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
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
                throw new Exception("Error: no pane selected");
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
                throw new Exception("Error: no pane selected");
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
            var copy = new CopyMoveViewModel(true, SelectedPane.CurrentItem, SecondPane.CurrentDirectory);            
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {            
                if (SelectedPane.CurrentItem.IsFolder)
                {
                    // copy folder
                    try
                    {
                        string destFolder = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                        CopyDirectory(SelectedPane.CurrentItem.FullName, destFolder, true);                        
                        SelectedPane.Update();
                        SecondPane.Update();
                    }
                    catch
                    {
                        MessageBox_Show(null, "Can't move folder here", "Alert");
                    }
                }
                else
                {
                    // copy file
                    string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                    if (destFile == SelectedPane.CurrentItem.FullName)
                    {
                        MessageBox_Show(null, "Can't copy file to itself", "Alert");
                    }
                    else if (!File.Exists(destFile))
                    {                        
                        File.Copy(SelectedPane.CurrentItem.FullName, destFile, false);
                        SelectedPane.Update();
                        SecondPane.Update();
                    }
                    else
                    {
                        MessageBox_Show(CopyFileExists, "File already exists. Are you sure you would like to rewrite " +
                            destFile + " ?", "Alert", ButtonEnum.YesNo);
                    }
                }               
            }
        }

        public void CopyFileExists(ButtonResult result)
        {
            if (result == ButtonResult.Yes)
            {
                string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                File.Copy(SelectedPane.CurrentItem.FullName, destFile, true);
                SelectedPane.Update();
                SecondPane.Update();
            }
        }      

        public async Task Move()
        {
            var copy = new CopyMoveViewModel(false, SelectedPane.CurrentItem, SecondPane.CurrentDirectory);
            var result = await ShowCopyDialog.Handle(copy);
            if (result != null)
            {
                if (SelectedPane.CurrentItem.IsFolder)
                {
                    // move folder
                    try
                    {
                        if (SelectedPane.CurrentItem.FullName == SecondPane.CurrentDirectory)
                        {
                            MessageBox_Show(null, "Can't move folder to itself", "Alert");
                            return;
                        }
                        string destFolder = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                        CopyDirectory(SelectedPane.CurrentItem.FullName, destFolder, true);
                        Directory.Delete(SelectedPane.CurrentItem.FullName, true);
                        SelectedPane.Update();
                        SecondPane.Update();
                    }
                    catch
                    {
                        MessageBox_Show(null, "Can't move folder here", "Alert");
                    }
                }
                else
                {
                    // move file
                    string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                    if (destFile == SelectedPane.CurrentItem.FullName)
                    {
                        MessageBox_Show(null, "Can't move file to itself", "Alert");
                    }
                    else if (!File.Exists(destFile))
                    {                        
                        File.Move(SelectedPane.CurrentItem.FullName, destFile, false);
                        SelectedPane.Update();
                        SecondPane.Update();
                    }
                    else
                    {
                        MessageBox_Show(MoveFileExists, "File already exists. Are you sure you would like to rewrite " +
                            destFile + " ?", "Alert", ButtonEnum.YesNo);
                    }
                }             
            }
        }

        public void MoveFileExists(ButtonResult result)
        {
            if (result == ButtonResult.Yes)
            {
                string destFile = Path.Combine(SecondPane.CurrentDirectory, Path.GetFileName(SelectedPane.CurrentItem.FullName));
                File.Move(SelectedPane.CurrentItem.FullName, destFile, true);
                SelectedPane.Update();
                SecondPane.Update();
            }
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
            var themeStyle = Application.Current.Styles[0];
            FluentTheme? fluentTheme = themeStyle as FluentTheme;
            if (fluentTheme == null)
                return;
            if (OptionsModel.Instance.IsDarkThemeEnabled)
            {                
                fluentTheme.Mode = FluentThemeMode.Dark;
            }
            else
            {
                fluentTheme.Mode = FluentThemeMode.Light;
            }
        }

        public void CreateNewFolder()
        {           
            MessageBoxInput_Show(CreateNewFolderAnswer, "Folder", "Create New Folder");            
        }

        public void CreateNewFolderAnswer(MessageWindowResultDTO result)
        {
            if (result.Button == "Confirm" && !string.IsNullOrEmpty(result.Message))
            {                
                SelectedPane.CreateNewFolder(result.Message);
                SelectedPane.Update();
                SecondPane.Update();
            }
        }

        public void Delete()
        {            
            MessageBox_Show(DeleteAnswer, "Are you sure you would like to delete " +
                SelectedPane.CurrentItem.Name + " ?", "Alert", ButtonEnum.YesNo);            
        }

        public void DeleteAnswer(ButtonResult result)
        {
            if (result == ButtonResult.Yes)
            {
                if (SelectedPane.NonEmptyFolder())
                {
                    MessageBox_Show(DeleteAnswerNonEmptyFolder, "The folder is not empty. Are you sure you would like to delete " +
                        SelectedPane.CurrentItem.Name + " ?", "Alert", ButtonEnum.YesNo);
                }
                else
                {
                    DeleteItem();
                }
            }
        }

        public void DeleteAnswerNonEmptyFolder(ButtonResult result)
        {
            if (result == ButtonResult.Yes)
            {
                DeleteItem();
            }
        }

        public void DeleteItem()
        {
            SelectedPane.Delete();
            SelectedPane.Update();
            SecondPane.Update();
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
