using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class FtpConnectViewModel : ViewModelBase
    {
        public ObservableCollection<FtpConnectionProfile> SavedConnections { get; }

        private FtpConnectionProfile? _selectedConnection;
        public FtpConnectionProfile? SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                _selectedConnection = value;
                this.RaisePropertyChanged(nameof(SelectedConnection));
                if (value != null)
                {
                    Host = value.Host;
                    Port = value.Port;
                    Username = value.Username;
                    Anonymous = value.Anonymous;
                }
            }
        }

        private string _host = "";
        public string Host
        {
            get => _host;
            set { _host = value; this.RaisePropertyChanged(nameof(Host)); }
        }

        private int _port = 21;
        public int Port
        {
            get => _port;
            set { _port = value; this.RaisePropertyChanged(nameof(Port)); }
        }

        private string _username = "";
        public string Username
        {
            get => _username;
            set { _username = value; this.RaisePropertyChanged(nameof(Username)); }
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set { _password = value; this.RaisePropertyChanged(nameof(Password)); }
        }

        private bool _anonymous;
        public bool Anonymous
        {
            get => _anonymous;
            set { _anonymous = value; this.RaisePropertyChanged(nameof(Anonymous)); }
        }

        public bool IsConfirmed { get; private set; }

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public FtpConnectViewModel()
        {
            SavedConnections = new ObservableCollection<FtpConnectionProfile>(FtpConnectionsModel.Instance.Connections);
            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);
        }

        private void SaveClose(Window window)
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                return;
            }

            var existing = FtpConnectionsModel.Instance.Connections.FirstOrDefault(c =>
                c.Host == Host && c.Port == Port && c.Username == Username && c.Anonymous == Anonymous);
            if (existing == null)
            {
                FtpConnectionsModel.Instance.Connections.Add(new FtpConnectionProfile
                {
                    Name = string.IsNullOrWhiteSpace(Username) ? Host : $"{Username}@{Host}",
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    Anonymous = Anonymous
                });
                FtpConnectionsModel.Instance.Save();
            }

            IsConfirmed = true;
            window?.Close(this);
        }

        private void Close(Window window)
        {
            window?.Close(this);
        }
    }
}
