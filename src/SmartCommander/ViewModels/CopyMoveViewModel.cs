using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Assets;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class CopyMoveViewModel : ViewModelBase
    {
        public CopyMoveViewModel(bool copy, string text, string directory)
        {
            IsCopying = copy;
            Text = text;
            Directory = directory;

            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);
        }

        public bool IsCopying { get; set; }

        public string Text { get;set;}

        public string Directory { get; set; }

        public string CopyText => IsCopying? string.Format(Resources.CopyTo, Text) :
            string.Format(Resources.MoveTo, Text);

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public void SaveClose(Window window)
        {
            window?.Close(this);
        }

        public void Close(Window window)
        {
            window?.Close(this);
        }
    }
}
