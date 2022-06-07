using System;
using System.Collections.Generic;
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

        public int CurrentIndex { get; set; } = -1;

        public List<int> SelectedIndices { get; set; } = new List<int> { };
      

        public List<string> FoldersFilesList { get; set; } = new List<string>();      

        public FilesPaneViewModel()
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        private void GetFilesFolders(string dir, List<string> filesFoldersList)
        {
            filesFoldersList.Clear();
            string[] subdirectoryEntries = Directory.GetDirectories(dir);
            foreach (string subdirectory in subdirectoryEntries)
            {
                filesFoldersList.Add(Path.GetFileName(subdirectory));
            }

            string[] fileEntries = Directory.GetFiles(dir);
            foreach (string fileName in fileEntries)
            {
                filesFoldersList.Add(Path.GetFileName(fileName));
            }
        }
    }
}
