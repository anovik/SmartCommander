namespace SmartCommander.ViewModels
{
    public class CopyMoveViewModel : ViewModelBase
    {
        public CopyMoveViewModel(bool copy, FileViewModel item, string directory)
        {
            IsCopying = copy;
            Item = item;
            Directory = directory;
        }

        public bool IsCopying { get; set; }

        public FileViewModel Item { get;set;}

        public string Directory { get; set; }
    }
}
