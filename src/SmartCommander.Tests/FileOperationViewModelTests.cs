using SmartCommander.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace SmartCommander.Tests
{
    public class FileOperationViewModelTests
    {
        // The internal ctor's post seam runs the progress callback synchronously,
        // so no Avalonia dispatcher is needed.
        private static FileOperationViewModel Create(string description = "op") =>
            new(description, action => action());

        private static List<int> RecordProgressChanges(FileOperationViewModel operation)
        {
            var values = new List<int>();
            ((INotifyPropertyChanged)operation).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileOperationViewModel.Progress))
                {
                    values.Add(operation.Progress);
                }
            };
            return values;
        }

        [Fact]
        public void Report_SameValueTwice_RaisesSingleProgressChange()
        {
            using var operation = Create();
            var changes = RecordProgressChanges(operation);

            operation.ProgressReporter.Report(42);
            operation.ProgressReporter.Report(42);

            Assert.Equal(new[] { 42 }, changes);
        }

        [Fact]
        public void Report_AfterDedupedValue_NewValueStillReported()
        {
            using var operation = Create();
            var changes = RecordProgressChanges(operation);

            operation.ProgressReporter.Report(5);
            operation.ProgressReporter.Report(5);
            operation.ProgressReporter.Report(7);

            Assert.Equal(new[] { 5, 7 }, changes);
        }

        [Fact]
        public void Report_OutOfRange_IsClamped()
        {
            using var operation = Create();

            operation.ProgressReporter.Report(150);
            Assert.Equal(100, operation.Progress);

            operation.ProgressReporter.Report(-5);
            Assert.Equal(0, operation.Progress);
        }

        [Fact]
        public void Description_IsExposed()
        {
            using var operation = Create("Copy 3 items → D:\\Backup");

            Assert.Equal("Copy 3 items → D:\\Backup", operation.Description);
        }

        [Fact]
        public void Cancel_SetsTokenCancellationRequested()
        {
            using var operation = Create();

            Assert.False(operation.Token.IsCancellationRequested);
            operation.Cancel();
            Assert.True(operation.Token.IsCancellationRequested);
        }

        [Fact]
        public void Cancel_AfterDispose_DoesNotThrow()
        {
            var operation = Create();
            operation.Dispose();

            operation.Cancel();
        }
    }
}
