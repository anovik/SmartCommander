using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using ReactiveUI;
using System;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {    
        public MainWindowViewModel()
        {
            ExitCommand = ReactiveCommand.Create(Exit);
            SortNameCommand = ReactiveCommand.Create(SortName);
            SortExtensionCommand = ReactiveCommand.Create(SortExtension);
            SortSizeCommand = ReactiveCommand.Create(SortSize);
            SortDateCommand = ReactiveCommand.Create(SortDate);
            EnterCommand = ReactiveCommand.Create(Execute);
            F3Command = ReactiveCommand.Create(View);
            F4Command = ReactiveCommand.Create(Edit);
            F5Command = ReactiveCommand.Create(Copy);
            F6Command = ReactiveCommand.Create(Move);
            F7Command = ReactiveCommand.Create(CreateNewFolder);
            F8Command = ReactiveCommand.Create(Delete);
            TabCommand = ReactiveCommand.Create(ChangeSelectedPane);
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

        public FilesPaneViewModel LeftFileViewModel { get; } = new FilesPaneViewModel() { IsSelected = true};

        public FilesPaneViewModel RightFileViewModel { get; } = new FilesPaneViewModel();

        private string? _commandText;
        public string? CommandText
        {
            get { return _commandText; }
            set
            {
                _commandText = value;              
                this.RaisePropertyChanged("CommandText");               
            }
        }


        public void Exit()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {                
                desktopLifetime.Shutdown();
            }
        }

        public void SortName()
        {

        }

        public void SortExtension()
        {

        }

        public void SortSize()
        {

        }

        public void SortDate()
        {

        }

        public void ChangeSelectedPane()
        {
            if (LeftFileViewModel.IsSelected)
            {
                LeftFileViewModel.IsSelected = false;
                RightFileViewModel.IsSelected = true;
            }
            else if (RightFileViewModel.IsSelected)
            {
                RightFileViewModel.IsSelected = false;
                LeftFileViewModel.IsSelected = true;                
            }
        }

        public void Execute()
        {
            FilesPaneViewModel pane = GetSelectedPane();           
            pane.Execute(CommandText);        

            CommandText = "";
        }

        public void View()
        {
            FilesPaneViewModel pane = GetSelectedPane();
            pane.View();
        }

        public void Edit()
        {
            FilesPaneViewModel pane = GetSelectedPane();
            pane.Edit();
        }

        public void Copy()
        {

        }

        public void Move()
        {

        }

        public void CreateNewFolder()
        {           
            MessageBoxInput_Show(CreateNewFolderAnswer, "Folder", "Create New Folder");            
        }

        public void CreateNewFolderAnswer(MessageWindowResultDTO result)
        {
            if (result.Button == "Confirm" && !string.IsNullOrEmpty(result.Message))
            {
                FilesPaneViewModel pane = GetSelectedPane();
                pane.CreateNewFolder(result.Message);
            }
        }

        public void Delete()
        {
            FilesPaneViewModel pane = GetSelectedPane();
            MessageBox_Show(DeleteAnswer, "Are you sure you would like to delete " + 
                pane.CurrentItem.Name + " ?", "Alert", ButtonEnum.YesNo);            
        }

        public void DeleteAnswer(ButtonResult result)
        {
            if (result == ButtonResult.Yes)
            {
                FilesPaneViewModel pane = GetSelectedPane();
                pane.Delete();
            }
        }

        private FilesPaneViewModel GetSelectedPane()
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
    }
}
