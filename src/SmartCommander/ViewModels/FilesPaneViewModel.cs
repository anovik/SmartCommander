using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class FilesPaneViewModel : ViewModelBase
    {
        private string _currentDirectory;
        public string CurrentDirectory
        {
            get { return _currentDirectory; }
            set { _currentDirectory = value; GetFilesFolders(CurrentDirectory, FoldersFilesList); }
        }

        private int _totalFiles = 0;
        private int _totalFolders = 0;

        public string CurrentDirectoryInfo
        {
            get { return string.Format("Files: {0}, folders: {1}.", _totalFiles, _totalFolders); }
        }

        public FileViewModel CurrentItem { get; set; }     

        public ObservableCollection<FileViewModel> FoldersFilesList { get; set; } = new ObservableCollection<FileViewModel>();      

        public FilesPaneViewModel()
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        public void CellPointerPressed(object sender, object parameter)
        {

        }

        public void DoubleTapped(object sender, object parameter)
        {
            string path = Path.Combine(CurrentDirectory, CurrentItem.Name);
            if (File.Exists(path))
            {
                // it is a file, open it
            }
            else if (Directory.Exists(path))
            {
                // go to this subdirectory
                CurrentDirectory = path;
            }
        }

        private void GetFilesFolders(string dir, ObservableCollection<FileViewModel> filesFoldersList)
        {
            if (!Directory.Exists(dir))
                return;
            filesFoldersList.Clear();
            string[] subdirectoryEntries = Directory.GetDirectories(dir);
            foreach (string subdirectory in subdirectoryEntries)
            {
                filesFoldersList.Add(new FileViewModel() { Name = Path.GetFileName(subdirectory) });
            }

            string[] fileEntries = Directory.GetFiles(dir);
            foreach (string fileName in fileEntries)
            {
                filesFoldersList.Add(new FileViewModel() { Name = Path.GetFileName(fileName) });
            }

            _totalFiles = subdirectoryEntries.Length;
            _totalFolders = fileEntries.Length;      
        }
    }
}
