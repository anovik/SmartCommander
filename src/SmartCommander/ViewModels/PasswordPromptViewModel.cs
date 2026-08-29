using Avalonia.Controls;
using ReactiveUI;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    // Prompt shown before extracting an encrypted archive. Re-shown in a loop
    // (with Retry = true) each time the entered password is rejected.
    public class PasswordPromptViewModel : ViewModelBase
    {
        public PasswordPromptViewModel(bool retry = false)
        {
            Retry = retry;
            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);
        }

        public bool Retry { get; }

        private string _password = "";
        public string Password
        {
            get => _password;
            set { _password = value; this.RaisePropertyChanged(nameof(Password)); }
        }

        private bool _revealPassword;
        public bool RevealPassword
        {
            get => _revealPassword;
            set { _revealPassword = value; this.RaisePropertyChanged(nameof(RevealPassword)); }
        }

        public bool IsConfirmed { get; private set; }

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        private void SaveClose(Window window)
        {
            IsConfirmed = true;
            window?.Close(this);
        }

        private void Close(Window window)
        {
            window?.Close(this);
        }
    }
}
