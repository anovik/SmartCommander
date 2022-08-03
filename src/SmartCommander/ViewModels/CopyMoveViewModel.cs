using Avalonia.Controls;
using ReactiveUI;
using System.Reactive;

namespace SmartCommander.ViewModels
{
    public class CopyMoveViewModel : ViewModelBase
    {
        public CopyMoveViewModel(bool copy, FileViewModel item, string directory)
        {
            IsCopying = copy;
            Item = item;
            Directory = directory;

            OKCommand = ReactiveCommand.Create<Window>(SaveClose);
            CancelCommand = ReactiveCommand.Create<Window>(Close);
        }

        public bool IsCopying { get; set; }

        public FileViewModel Item { get;set;}

        public string Directory { get; set; }

        public string CopyText => (IsCopying? "Copy '" : "Move '") + Item.Name + "' to:";

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public void SaveClose(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        public void Close(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }
    }
}
