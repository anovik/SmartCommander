using Avalonia.Threading;
using ReactiveUI;
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
    private Timer _timer;

    public string CurrentFolder
    {
        get => _currentFolder;
        set => this.RaiseAndSetIfChanged(ref _currentFolder, value);
    }

    public string StatusFolder
    {
        get => _statusFolder;
        set => this.RaiseAndSetIfChanged(ref _statusFolder, value);
    }

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

    public BulkObservableCollection<string> SearchResults { get; }
    public ReactiveCommand<Unit, Unit> StartSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }

    public FileSearchViewModel(string folder = "")
    {
        CurrentFolder = folder ?? "c:\\";
        FileMask = "*.cs";
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
            List<string> findedFolderAndFiles= new List<string>();

            var dirs = Directory.GetDirectories(folderPath, searchPattern, SearchOption.TopDirectoryOnly);
            findedFolderAndFiles.AddRange(dirs);

            string[] files = Directory.GetFiles(folderPath, searchPattern);
            findedFolderAndFiles.AddRange(files);

            string[] subDirectories = Directory.GetDirectories(folderPath);


            SearchResults.AddRange(findedFolderAndFiles);
            foreach (var subDir in subDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SearchAsync(subDir, searchPattern, cancellationToken);
            }
        }

        catch (OperationCanceledException)
        {
            Console.WriteLine("Поиск был отменен.");
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine($"Доступ к директории {folderPath} запрещен: {e.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при поиске: {ex.Message}");
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
        _timer = new Timer(OnTimerTick, _statusFolder, 0, 100);
        await Task.Run(() => SearchAsync(CurrentFolder, FileMask, _cancellationTokenSource.Token));

        IsSearching = false;
    }

    private void OnTimerTick(object state)
    {
        if (!string.IsNullOrEmpty((string)state))
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusFolder = (string)state;
            });
        }
    }

    private void CancelSearch()
    {
        IsSearching = false;
        _timer?.Dispose();
        _cancellationTokenSource?.Cancel();
    }
}
