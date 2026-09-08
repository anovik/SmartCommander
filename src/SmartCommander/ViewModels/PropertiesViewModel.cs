using Avalonia.Controls;
using ReactiveUI;
using Serilog;
using SmartCommander.Assets;
using SmartCommander.Services;
using System;
using System.Globalization;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace SmartCommander.ViewModels
{
    // View a single local file/folder's metadata and toggle its Read-only (all platforms) and
    // Hidden (Windows only) attributes. For a folder, the recursive size is walked off-thread
    // and can be cancelled. No ActiveOperations entry - it's self-contained and read-mostly.
    public class PropertiesViewModel : ViewModelBase
    {
        private readonly PropertiesService _service;
        private readonly string _path;
        private readonly bool _isFolder;
        private readonly CancellationTokenSource _sizeCts = new();

        private bool _initialReadOnly;
        private bool _initialHidden;
        private bool _loaded;

        public PropertiesViewModel(string path, bool isFolder, PropertiesService service)
            : this(path, isFolder, service, load: true)
        {
        }

        // Design-time only: no file I/O.
        public PropertiesViewModel()
            : this(Path.Combine("C:", "folder", "file.txt"), false, new PropertiesService(), load: false)
        {
        }

        private PropertiesViewModel(string path, bool isFolder, PropertiesService service, bool load)
        {
            _path = path;
            _isFolder = isFolder;
            _service = service;

            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            ItemName = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(ItemName))
            {
                ItemName = path;
            }
            Location = Path.GetDirectoryName(path) ?? path;
            TypeText = isFolder ? Resources.PropertiesTypeFolder : Resources.PropertiesTypeFile;

            ApplyCommand = ReactiveCommand.CreateFromTask<Window>(ApplyAsync, this.WhenAnyValue(x => x.IsDirty));
            CancelSizeCommand = ReactiveCommand.Create(() => _sizeCts.Cancel());
            CloseCommand = ReactiveCommand.Create<Window>(w => { _sizeCts.Cancel(); w?.Close(); });

            if (load)
            {
                _ = LoadAsync();
            }
        }

        public string ItemName { get; }
        public string Location { get; }
        public string TypeText { get; }
        public bool IsFolder => _isFolder;
        public bool CanEditHidden => OperatingSystem.IsWindows();

        private string _sizeText = "";
        public string SizeText { get => _sizeText; private set => this.RaiseAndSetIfChanged(ref _sizeText, value); }

        private string _containsText = "";
        public string ContainsText { get => _containsText; private set => this.RaiseAndSetIfChanged(ref _containsText, value); }

        private bool _isCalculatingSize;
        public bool IsCalculatingSize { get => _isCalculatingSize; private set => this.RaiseAndSetIfChanged(ref _isCalculatingSize, value); }

        private string _created = "";
        public string Created { get => _created; private set => this.RaiseAndSetIfChanged(ref _created, value); }

        private string _modified = "";
        public string Modified { get => _modified; private set => this.RaiseAndSetIfChanged(ref _modified, value); }

        private string _accessed = "";
        public string Accessed { get => _accessed; private set => this.RaiseAndSetIfChanged(ref _accessed, value); }

        private bool _readOnly;
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                this.RaiseAndSetIfChanged(ref _readOnly, value);
                this.RaisePropertyChanged(nameof(IsDirty));
            }
        }

        private bool _hidden;
        public bool Hidden
        {
            get => _hidden;
            set
            {
                this.RaiseAndSetIfChanged(ref _hidden, value);
                this.RaisePropertyChanged(nameof(IsDirty));
            }
        }

        public bool IsDirty => _loaded &&
            (_readOnly != _initialReadOnly || (CanEditHidden && _hidden != _initialHidden));

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

        public ReactiveCommand<Window, Unit> ApplyCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelSizeCommand { get; }
        public ReactiveCommand<Window, Unit> CloseCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                var p = await _service.GetAsync(_path, _isFolder);

                _initialReadOnly = p.IsReadOnly;
                _initialHidden = p.IsHidden;
                _readOnly = p.IsReadOnly;
                _hidden = p.IsHidden;
                this.RaisePropertyChanged(nameof(ReadOnly));
                this.RaisePropertyChanged(nameof(Hidden));

                Created = p.CreationTime.ToString("G", CultureInfo.CurrentCulture);
                Modified = p.LastWriteTime.ToString("G", CultureInfo.CurrentCulture);
                Accessed = p.LastAccessTime.ToString("G", CultureInfo.CurrentCulture);

                if (_isFolder)
                {
                    await ComputeSizeAsync();
                }
                else
                {
                    SizeText = FormatSize(p.Size);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Properties load failed for {Path}", _path);
                Status = DescribeException(ex);
            }
            finally
            {
                _loaded = true;
                this.RaisePropertyChanged(nameof(IsDirty));
            }
        }

        private async Task ComputeSizeAsync()
        {
            IsCalculatingSize = true;
            SizeText = Resources.PropertiesCalculating;
            try
            {
                var progress = new Progress<FolderTally>(t =>
                {
                    SizeText = FormatSize(t.Bytes);
                    ContainsText = string.Format(Resources.PropertiesContainsFormat, t.Files, t.Folders);
                });
                var tally = await _service.ComputeFolderTallyAsync(_path, progress, _sizeCts.Token);
                SizeText = FormatSize(tally.Bytes);
                ContainsText = string.Format(Resources.PropertiesContainsFormat, tally.Files, tally.Folders);
            }
            catch (OperationCanceledException)
            {
                if (SizeText == Resources.PropertiesCalculating)
                {
                    SizeText = Resources.PropertiesSizeCancelled;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Folder size walk failed for {Path}", _path);
                Status = DescribeException(ex);
            }
            finally
            {
                IsCalculatingSize = false;
            }
        }

        private async Task ApplyAsync(Window? window)
        {
            try
            {
                await _service.ApplyAttributesAsync(_path, _readOnly, _hidden);
                _initialReadOnly = _readOnly;
                _initialHidden = _hidden;
                this.RaisePropertyChanged(nameof(IsDirty));
                window?.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Applying attributes failed for {Path}", _path);
                Status = string.Format(Resources.PropertiesApplyFailed, DescribeException(ex));
            }
        }

        // "82.3 MB (86,240,133 bytes)" - human-readable value plus the exact byte count.
        internal static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            var human = unit == 0
                ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", value, units[unit])
                : string.Format(CultureInfo.CurrentCulture, "{0:N1} {1}", value, units[unit]);
            return string.Format(Resources.PropertiesSizeFormat, human, bytes);
        }
    }
}
