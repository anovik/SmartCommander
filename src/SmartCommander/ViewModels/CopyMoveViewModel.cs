using Avalonia.Controls;
using ReactiveUI;
using SmartCommander.Assets;
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

        public string CopyText => (IsCopying? string.Format(Resources.CopyTo, Item.Name) :
            string.Format(Resources.MoveTo, Item.Name));

        public ReactiveCommand<Window, Unit> OKCommand { get; }
        public ReactiveCommand<Window, Unit> CancelCommand { get; }

        public void SaveClose(Window window)
        {
            if (window != null)
            {
                window.Close(this);
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
