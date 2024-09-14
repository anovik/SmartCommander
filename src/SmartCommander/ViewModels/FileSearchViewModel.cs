using Avalonia.Collections;
using ReactiveUI;
using SmartCommander.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

public class FileSearchViewModel : ViewModelBase
{
    private string _currentFolder;
    private string _fileMask;
    private bool _isSearching;

    // Свойство для поля текущей папки
    public string CurrentFolder
    {
        get => _currentFolder;
        set => this.RaiseAndSetIfChanged(ref _currentFolder, value);
    }

    // Свойство для поля названия файла (с поддержкой маски)
    public string FileMask
    {
        get => _fileMask;
        set => this.RaiseAndSetIfChanged(ref _fileMask, value);
    }

    // Свойство для контроля состояния поиска
    public bool IsSearching
    {
        get => _isSearching;
        set => this.RaiseAndSetIfChanged(ref _isSearching, value);
    }

    // Список результатов поиска
    public ObservableCollection<string> SearchResults { get; }

    // Команды для кнопок
    public ReactiveCommand<Unit, Unit> StartSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }

    public FileSearchViewModel()
    {
        // Инициализируем список результатов
        SearchResults = new ObservableCollection<string>();

        // Инициализируем команды
        StartSearchCommand = ReactiveCommand.CreateFromTask(StartSearch);
        CancelSearchCommand = ReactiveCommand.Create(CancelSearch);
    }

    // Метод для начала поиска
    private async Task StartSearch()
    {
        if (string.IsNullOrEmpty(CurrentFolder) || string.IsNullOrEmpty(FileMask))
            return;

        IsSearching = true;
        SearchResults.Clear();

        try
        {
            // Асинхронный поиск файлов и папок
            await Task.Run(() => SearchFiles(CurrentFolder, FileMask));
        }
        finally
        {
            IsSearching = false;
        }
    }

    // Реализация поиска файлов и папок по маске
    private void SearchFiles(string folder, string mask)
    {
        try
        {
            // Получаем все файлы и папки по маске
            foreach (var dir in Directory.GetDirectories(folder))
            {
                SearchResults.Add(dir);
            }

            foreach (var file in Directory.GetFiles(folder, mask))
            {
                SearchResults.Add(file);
            }
        }
        catch (Exception ex)
        {
            // В случае ошибки можно обработать её
            SearchResults.Add($"Ошибка: {ex.Message}");
        }
    }

    // Метод для отмены поиска
    private void CancelSearch()
    {
        // Логика отмены, если потребуется
        IsSearching = false;
    }
}
