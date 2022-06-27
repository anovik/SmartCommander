using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
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
        }      

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        public ReactiveCommand<Unit, Unit> SortNameCommand { get; }
        public ReactiveCommand<Unit, Unit> SortExtensionCommand { get; }
        public ReactiveCommand<Unit, Unit> SortSizeCommand { get; }
        public ReactiveCommand<Unit, Unit> SortDateCommand { get; }
        public ReactiveCommand<Unit, Unit> EnterCommand { get; }

        public FilesPaneViewModel LeftFileViewModel { get; } = new FilesPaneViewModel() { IsSelected = true};

        public FilesPaneViewModel RightFileViewModel { get; } = new FilesPaneViewModel();

        private string _commandText;
        public string CommandText
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

        public void Execute()
        {
            if (LeftFileViewModel.IsSelected)
            {
                LeftFileViewModel.Execute(CommandText);
            }
            else if (RightFileViewModel.IsSelected)
            {
                RightFileViewModel.Execute(CommandText);
            }

            CommandText = "";
        }
    }
}
