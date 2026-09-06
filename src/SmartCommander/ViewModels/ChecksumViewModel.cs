using Avalonia.Controls;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Services;
using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace SmartCommander.ViewModels
{
    public enum ChecksumMatch { None, Match, NoMatch }

    // Read-only dialog: computes MD5 / SHA-1 / SHA-256 for a single local file and, if an
    // "Expected value" is supplied (typed, or pre-filled from a sidecar), reports whether it
    // matches any of the three. Computation is streamed off-thread and can be cancelled.
    public class ChecksumViewModel : ViewModelBase
    {
        private readonly ChecksumService _service;
        private readonly string _filePath;
        private readonly CancellationTokenSource _cts = new();

        public ChecksumViewModel(string filePath, ChecksumService service)
            : this(filePath, service, compute: true)
        {
        }

        // Design-time only: no file I/O, no background computation.
        public ChecksumViewModel()
            : this("file.bin", new ChecksumService(), compute: false)
        {
        }

        private ChecksumViewModel(string filePath, ChecksumService service, bool compute)
        {
            _filePath = filePath;
            _service = service;
            FileName = Path.GetFileName(filePath);

            CancelCommand = ReactiveCommand.Create(() => _cts.Cancel());
            CloseCommand = ReactiveCommand.Create<Window>(w => { _cts.Cancel(); w?.Close(); });

            if (compute)
            {
                _ = RunAsync();
            }
            else
            {
                IsComputing = false;
            }
        }

        public string FileName { get; }

        private string _md5 = "";
        public string Md5 { get => _md5; private set => this.RaiseAndSetIfChanged(ref _md5, value); }

        private string _sha1 = "";
        public string Sha1 { get => _sha1; private set => this.RaiseAndSetIfChanged(ref _sha1, value); }

        private string _sha256 = "";
        public string Sha256 { get => _sha256; private set => this.RaiseAndSetIfChanged(ref _sha256, value); }

        private int _progress;
        public int Progress { get => _progress; private set => this.RaiseAndSetIfChanged(ref _progress, value); }

        private bool _isComputing = true;
        public bool IsComputing
        {
            get => _isComputing;
            private set => this.RaiseAndSetIfChanged(ref _isComputing, value);
        }

        private string _status = "";
        public string Status
        {
            get => _status;
            private set
            {
                this.RaiseAndSetIfChanged(ref _status, value);
                this.RaisePropertyChanged(nameof(HasStatus));
            }
        }
        public bool HasStatus => !string.IsNullOrEmpty(Status);

        private string _expectedValue = "";
        public string ExpectedValue
        {
            get => _expectedValue;
            set
            {
                this.RaiseAndSetIfChanged(ref _expectedValue, value);
                UpdateMatch();
            }
        }

        private ChecksumMatch _matchResult = ChecksumMatch.None;
        public ChecksumMatch MatchResult
        {
            get => _matchResult;
            private set
            {
                this.RaiseAndSetIfChanged(ref _matchResult, value);
                this.RaisePropertyChanged(nameof(ShowMatch));
                this.RaisePropertyChanged(nameof(ShowNoMatch));
                this.RaisePropertyChanged(nameof(MatchText));
            }
        }

        public bool ShowMatch => MatchResult == ChecksumMatch.Match;
        public bool ShowNoMatch => MatchResult == ChecksumMatch.NoMatch;

        private string _matchedAlgorithm = "";
        public string MatchText => MatchResult == ChecksumMatch.Match
            ? string.Format(Resources.ChecksumMatches, _matchedAlgorithm)
            : Resources.ChecksumNoMatch;

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Window, Unit> CloseCommand { get; }

        private async Task RunAsync()
        {
            try
            {
                var sidecar = await _service.TryReadSidecarAsync(_filePath, _cts.Token);
                if (sidecar != null && string.IsNullOrEmpty(ExpectedValue))
                {
                    ExpectedValue = sidecar;
                }

                var progress = new Progress<int>(p => Progress = p);
                var result = await _service.ComputeAsync(_filePath, progress, _cts.Token);
                Md5 = result.Md5;
                Sha1 = result.Sha1;
                Sha256 = result.Sha256;
            }
            catch (OperationCanceledException)
            {
                Status = Resources.ChecksumCancelled;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Checksum computation failed for {FilePath}", _filePath);
                Status = DescribeException(ex);
            }
            finally
            {
                IsComputing = false;
                UpdateMatch();
            }
        }

        private void UpdateMatch()
        {
            var expected = (ExpectedValue ?? "").Trim();
            if (expected.Length == 0 || string.IsNullOrEmpty(Sha256))
            {
                MatchResult = ChecksumMatch.None;
                return;
            }

            // A pasted sidecar line is often "<hash> *filename"; compare on the first token.
            var spaceIdx = expected.IndexOfAny(new[] { ' ', '\t' });
            var token = spaceIdx >= 0 ? expected.Substring(0, spaceIdx) : expected;

            if (token.Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            {
                _matchedAlgorithm = "SHA-256";
                MatchResult = ChecksumMatch.Match;
            }
            else if (token.Equals(Sha1, StringComparison.OrdinalIgnoreCase))
            {
                _matchedAlgorithm = "SHA-1";
                MatchResult = ChecksumMatch.Match;
            }
            else if (token.Equals(Md5, StringComparison.OrdinalIgnoreCase))
            {
                _matchedAlgorithm = "MD5";
                MatchResult = ChecksumMatch.Match;
            }
            else
            {
                MatchResult = ChecksumMatch.NoMatch;
            }
        }
    }
}
