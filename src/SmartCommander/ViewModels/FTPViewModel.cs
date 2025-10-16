using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SmartCommander.ViewModels
{
    public class FtpViewModel : ViewModelBase
    {
        public ObservableCollection<Ftp> Ftps { get; set; } = [];
        private string? _ftpName;
        public string? FtpName
        {
            get => _ftpName;
            set => this.RaiseAndSetIfChanged(ref _ftpName, value);
        }

        private bool _isAnonymous;
        public bool IsAnonymous
        {
            get => _isAnonymous;
            set => this.RaiseAndSetIfChanged(ref _isAnonymous, value);
        }

        private string? _userName;
        public string? UserName
        {
            get => _userName;
            set => this.RaiseAndSetIfChanged(ref _userName, value);
        }

        private string? _password;
        public string? Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        private Ftp? _selectedFtp;
        public Ftp? SelectedFtp
        {
            get => _selectedFtp;
            set
            {              
                this.RaiseAndSetIfChanged(ref _selectedFtp, value);

                if (value != null)
                {
                    FtpName = value.FtpName;
                    IsAnonymous = value.IsAnonymous;
                    UserName = value.UserName;
                    Password = value.Password;
                }
                else
                {                 
                    FtpName = null;
                    IsAnonymous = false;
                    UserName = null;
                    Password = null;
                }
            }
        }

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public FtpViewModel()
        {
            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);

            Ftps.Clear();
            foreach (var ftp in FtpModel.Instance.Ftps)
            {
                Ftps.Add(ftp);
            }          
        }

        public void SaveClose(Window window)
        {
            if (string.IsNullOrWhiteSpace(FtpName))
            {               
                return;
            }
          
            var existing = FtpModel.Instance.Ftps.FirstOrDefault(f => f.FtpName == FtpName);

            if (existing != null)
            {             
                existing.IsAnonymous = IsAnonymous;
                existing.UserName = UserName;
                existing.Password = Password;
            }
            else
            {                
                var newFtp = new Ftp(FtpName)
                {
                    IsAnonymous = IsAnonymous,
                    UserName = UserName,
                    Password = Password
                };

                FtpModel.Instance.Ftps.Add(newFtp);
                Ftps.Add(newFtp); 
            }
         
            FtpModel.Instance.Save();

            window?.Close(this);

        }

        public void Close(Window window)
        {
            window?.Close();
        }
    }
}
