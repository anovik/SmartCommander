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

            LeftFileViewModel = new FilesPaneViewModel(this) { IsSelected = true };
            RightFileViewModel = new FilesPaneViewModel(this);

            ShowCopyDialog = new Interaction<CopyMoveViewModel, CopyMoveViewModel?>();
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

        public FilesPaneViewModel LeftFileViewModel { get; } 

        public FilesPaneViewModel RightFileViewModel { get; }

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

        public Interaction<CopyMoveViewModel, CopyMoveViewModel?> ShowCopyDialog { get; }

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
                SelectedPane = RightFileViewModel;
            }
            else if (RightFileViewModel.IsSelected)
            {
                SelectedPane = LeftFileViewModel;       
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

        public void Copy()
        {
            var copy = new CopyMoveViewModel(true, SelectedPane.CurrentItem, "");            
            var result = ShowCopyDialog.Handle(copy);
            // do something here
        }

        public void Move()
        {
            var copy = new CopyMoveViewModel(false, SelectedPane.CurrentItem, "");
            var result = ShowCopyDialog.Handle(copy);
            // do something here
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
                SelectedPane.Delete();
            }
        }       
    }
}
