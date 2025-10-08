using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Models;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class FtpViewModel : ViewModelBase
    {
        public ObservableCollection<Ftp> Ftps { get; set; } = [];
        public string? FtpName { get; private set; }
        public bool IsAnonymous { get; private set; }
        public string? UserName { get; private set; }
        public string? Password { get; private set; }

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public FtpViewModel()
        {
            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);

            // TODO: load data from model
        }

        public void SaveClose(Window window)
        {
            // TODO: save data to model
           
            window?.Close(this);

        }

        public void Close(Window window)
        {
            window?.Close();
        }
    }
}
