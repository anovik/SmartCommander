using System;
using System.Collections.Generic;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private string _currentDirectory1;
        public string CurrentDirectory1
        {
            get { return _currentDirectory1; }
            set { _currentDirectory1 = value; GetFilesFolders(CurrentDirectory1, FoldersFilesList1); }
        }

        private string _currentDirectory2;
        public string CurrentDirectory2
        {
            get { return _currentDirectory2; }
            set { _currentDirectory2 = value; GetFilesFolders(CurrentDirectory2, FoldersFilesList2); }
        }

        public List<string> FoldersFilesList1 { get; set; } = new List<string>();

        public List<string> FoldersFilesList2 { get; set; } = new List<string>();

        public MainWindowViewModel()
        {
            CurrentDirectory1 = CurrentDirectory2
                = Environment.GetFolderPath(Environment.SpecialFolder.Personal);     
            
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
