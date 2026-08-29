using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Assets;
using SmartCommander.Services;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;

namespace SmartCommander.ViewModels
{
    // One row in the compression-level dropdown: the enum value plus its localized label.
    public class ZipLevelOption
    {
        public ZipLevelOption(ZipCompressionLevel level, string name)
        {
            Level = level;
            Name = name;
        }

        public ZipCompressionLevel Level { get; }
        public string Name { get; }
    }

    public class ZipOptionsViewModel : ViewModelBase
    {
        public ZipOptionsViewModel()
        {
            LevelOptions = new List<ZipLevelOption>
            {
                new(ZipCompressionLevel.Store, Resources.CompressionStore),
                new(ZipCompressionLevel.Fastest, Resources.CompressionFastest),
                new(ZipCompressionLevel.Normal, Resources.CompressionNormal),
                new(ZipCompressionLevel.Maximum, Resources.CompressionMaximum)
            };
            _selectedLevel = LevelOptions[2]; // Normal

            var canOk = this.WhenAnyValue(x => x.Password, x => x.ConfirmPassword, (p, c) => p == c);
            OKCommand = ReactiveCommand.Create<Window>(SaveClose, canOk);
            CancelCommand = ReactiveCommand.Create<Window>(Close);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                this.RaisePropertyChanged(nameof(Password));
                this.RaisePropertyChanged(nameof(ShowMismatch));
            }
        }

        private bool _revealPassword;
        public bool RevealPassword
        {
            get => _revealPassword;
            set { _revealPassword = value; this.RaisePropertyChanged(nameof(RevealPassword)); }
        }

        private string _confirmPassword = "";
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                _confirmPassword = value;
                this.RaisePropertyChanged(nameof(ConfirmPassword));
                this.RaisePropertyChanged(nameof(ShowMismatch));
            }
        }

        public IReadOnlyList<ZipLevelOption> LevelOptions { get; }

        private ZipLevelOption _selectedLevel;
        public ZipLevelOption SelectedLevel
        {
            get => _selectedLevel;
            set { _selectedLevel = value; this.RaisePropertyChanged(nameof(SelectedLevel)); }
        }

        // Shown inline in red once the confirm field has content that doesn't match.
        public bool ShowMismatch => !string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword;

        public ZipCompressionLevel Level => SelectedLevel.Level;

        // Empty password => plain (unencrypted) zip.
        public string? PasswordOrNull => string.IsNullOrEmpty(Password) ? null : Password;

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
