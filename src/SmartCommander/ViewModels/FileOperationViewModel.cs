using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading;

namespace SmartCommander.ViewModels
{
    // One row in the Operations window: a single long-running file operation with its own
    // progress and cancellation. Created, added to ActiveOperations, and disposed exclusively
    // on the UI thread by MainWindowViewModel.RunOperationAsync.
    public class FileOperationViewModel : ReactiveObject, IDisposable
    {
        private readonly SmartCancellationTokenSource _tokenSource = new();
        private readonly Action<Action> _post;
        private int _progress;

        public FileOperationViewModel(string description)
            : this(description, action => Dispatcher.UIThread.Post(action))
        {
        }

        // The post seam lets tests run the progress callback synchronously without a dispatcher.
        internal FileOperationViewModel(string description, Action<Action> post)
        {
            Description = description;
            _post = post;
            ProgressReporter = new FilteringProgress(OnProgressReported);
            CancelCommand = ReactiveCommand.Create(Cancel);
        }

        public string Description { get; }

        public int Progress
        {
            get => _progress;
            private set => this.RaiseAndSetIfChanged(ref _progress, value);
        }

        public IProgress<int> ProgressReporter { get; }

        public CancellationToken Token => _tokenSource.Token;

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public void Cancel()
        {
            // A Cancel click can race with operation completion: Dispose runs on the UI thread
            // right after the operation's await, so a late click must not touch a disposed source.
            if (!_tokenSource.IsDisposed)
            {
                _tokenSource.Cancel();
            }
        }

        public void Dispose()
        {
            _tokenSource.Dispose();
        }

        private void OnProgressReported(int value)
        {
            _post(() => Progress = Math.Clamp(value, 0, 100));
        }

        private sealed class FilteringProgress : IProgress<int>
        {
            private volatile int _last = -1;
            private readonly Action<int> _callback;
            internal FilteringProgress(Action<int> callback) => _callback = callback;
            public void Report(int value)
            {
                if (value == _last) return;
                _last = value;
                _callback(value);
            }
        }
    }
}
