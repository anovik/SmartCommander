using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using SmartCommander.Extensions;
using SmartCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

public class FileSearchViewModel : ViewModelBase
{
    private string _currentFolder = "";
    private string _statusFolder = "";
    private string _fileMask = "";
    private bool _isSearching = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private Timer? _timer;

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

    public FileSearchViewModel(string folder = "")
    {
        CurrentFolder = folder ?? "c:\\";
        FileMask = "*.txt";
        SearchResults = new BulkObservableCollection<string>();
        StartSearchCommand = ReactiveCommand.CreateFromTask(StartSearch);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
    }

    public async Task<bool> SearchAsync(string folderPath, string searchPattern, CancellationToken cancellationToken)
    {      
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _statusFolder = folderPath;
            var findedFolderAndFiles = new List<string>();

            if (SearchContent)
            {
                var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);
                foreach(var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (string line in File.ReadLines(file))
                    {
                        if (line.Contains(searchPattern))
                        {
                            SearchResults.Add(file);
                            break;
                        }
                    }
                }
               
            }
            else
            {
                var dirs = Directory.GetDirectories(folderPath, searchPattern, SearchOption.TopDirectoryOnly);
                findedFolderAndFiles.AddRange(dirs);
                var files = Directory.GetFiles(folderPath, searchPattern);
                findedFolderAndFiles.AddRange(files);
                SearchResults.AddRange(findedFolderAndFiles);
            }         

            if (!TopDirectoryOnly)
            {
                var subDirectories = Directory.GetDirectories(folderPath);
                foreach (var subDir in subDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SearchAsync(subDir, searchPattern, cancellationToken);
                }
            }
        }     
        catch (OperationCanceledException e)
        {
            Log.Error("OperationCanceledException: " + e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            Log.Error("UnauthorizedAccessException: " + e.Message);
        }        
        catch (Exception e)
        {
            Log.Error("Exception: " + e.Message);
        }
        
        return true;
    }

    private async Task StartSearch()
    {
        if (string.IsNullOrEmpty(CurrentFolder) || string.IsNullOrEmpty(FileMask))
            return;

        IsSearching = true;
        SearchResults.Clear();
        _cancellationTokenSource = new CancellationTokenSource();
        _timer = new Timer(OnTimerTick, null, 0, 500);
        await Task.Run(() => SearchAsync(CurrentFolder, SearchContent ? SearchText :  FileMask, _cancellationTokenSource.Token));

        _statusFolder = "";
        IsSearching = false;
    }

    private void OnTimerTick(object? state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusFolder = _statusFolder;
            this.RaisePropertyChanged(nameof(StatusFolder));
        });
    }

    public void CancelSearch()
    {
        _cancellationTokenSource?.Cancel();
        IsSearching = false;
        _timer?.Dispose();
    }
}
