using SmartCommander.ViewModels;
using System.Collections.Generic;
using System.IO;

namespace SmartCommander
{
    static internal class Utils
    {
        static internal void DeleteDirectoryWithHiddenFiles(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);
        }

        static internal void SetNormalFileAttributes(string path)
        {          
            if (!File.Exists(path))
            {
                return;
            }
            FileInfo fileInfo = new FileInfo(path);
            fileInfo.Attributes = FileAttributes.Normal;
           
        }

        static internal List<string> GetDuplicates(List<FileViewModel> selectedItems, string destpath)
        {
            var duplicates = new List<string>();

            // TODO: iterate through selectedItems

            return duplicates;
        }

        static internal List<string> GetNonEmptyFolders(List<FileViewModel> selectedItems)
        {
            var nonEmptyFolders = new List<string>();

            // TODO: iterate through selectedItems

            return nonEmptyFolders;
        }

        static internal void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                if (File.Exists(targetFilePath))
                {
                    Utils.SetNormalFileAttributes(targetFilePath);
                }
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
