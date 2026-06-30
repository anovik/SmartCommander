using ReactiveUI;
using SmartCommander.Extensions;
using SmartCommander.Services;
using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.ViewModels
{
    public class FileSearchViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fs;
        private string _currentFolder = "";
        private string _fileMask = "";
        private bool _isSearching = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public string CurrentFolder
        {
            get => _currentFolder;
            set => this.RaiseAndSetIfChanged(ref _currentFolder, value);
        }

        public string StatusFolder { get; set; } = "";

        public bool TopDirectoryOnly { get; set; }

        public bool SearchContent { get; set; }

        public string SearchText { get; set; } = "";

        public string FileMask
        {
            get => _fileMask;
            set => this.RaiseAndSetIfChanged(ref _fileMask, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => this.RaiseAndSetIfChanged(ref _isSearching, value);
        }

        public string ResultFilename { get; set; } = string.Empty;

        public BulkObservableCollection<string> SearchResults { get; }
        public ReactiveCommand<Unit, Unit> StartSearchCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }

        public FileSearchViewModel(string folder, IFileSystemService fs)
        {
            _fs = fs;
            CurrentFolder = folder;
            FileMask = "*.txt";
            SearchResults = new BulkObservableCollection<string>();
            StartSearchCommand = ReactiveCommand.CreateFromTask(StartSearch);
            CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
        }

        private async Task StartSearch()
        {
            if (string.IsNullOrEmpty(CurrentFolder) || string.IsNullOrEmpty(FileMask))
            {
                return;
            }

            IsSearching = true;
            SearchResults.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            var resultsProgress = new Progress<string>(result => SearchResults.Add(result));
            var statusProgress = new Progress<string>(path =>
            {
                StatusFolder = path;
                this.RaisePropertyChanged(nameof(StatusFolder));
            });

            try
            {
                await _fs.SearchAsync(
                    CurrentFolder,
                    FileMask,
                    TopDirectoryOnly,
                    SearchContent,
                    SearchText,
                    resultsProgress,
                    statusProgress,
                    _cancellationTokenSource.Token);
            }
            catch (System.OperationCanceledException)
            {
                // expected when user cancels
            }
            finally
            {
                StatusFolder = "";
                this.RaisePropertyChanged(nameof(StatusFolder));
                IsSearching = false;
            }
        }

        public void CancelSearch()
        {
            _cancellationTokenSource?.Cancel();
            IsSearching = false;
            StatusFolder = "";
            this.RaisePropertyChanged(nameof(StatusFolder));
        }
    }
}
