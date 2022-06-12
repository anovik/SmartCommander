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
        }      

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }


        public void Exit()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {                
                desktopLifetime.Shutdown();
            }
        }
    }
}
