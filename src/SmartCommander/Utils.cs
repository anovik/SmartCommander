using SmartCommander.Models;
using SmartCommander.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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

            foreach (var item in selectedItems)
            {
                string targetFilePath = Path.Combine(destpath, Path.GetFileName(item.FullName));
                if (item.IsFolder)
                {
                    EnumerateDuplicates(item.FullName, targetFilePath, ref duplicates);
                }
                else
                {                    
                    if (File.Exists(targetFilePath))
                    {
                        duplicates.Add(item.FullName);
                    }
                }
            }

            return duplicates;
        }

        static internal List<string> GetNonEmptyFolders(List<FileViewModel> selectedItems)
        {           
            var nonEmptyFolders = new List<string>();

            if (!OptionsModel.Instance.ConfirmationWhenDeleteNonEmpty)
            {
                return nonEmptyFolders;
            }          

            foreach (var item in selectedItems)
            {
               if (item.IsFolder && !IsDirectoryEmpty(item.FullName))
                {
                    nonEmptyFolders.Add(item.FullName);
                }
            }

            return nonEmptyFolders;
        }
        
        static internal bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        static internal void CopyFile(string source, string dest, bool delete, bool overwrite, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }

            if (!overwrite)
            {
                // TODO: check if file exists, then return
            }

            if (delete)
            {
                // TODO: check if files on the same drive, then just move and return
            }

            // TODO: copy file in chunks

            if (delete)
            {
                //File.Delete(dest);
            }
        }

        // TODO: maybe need to pass overwrite
        static internal void CopyDirectory(string sourceDir, string destinationDir, bool recursive, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }
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
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                if (File.Exists(targetFilePath))
                {
                    Utils.SetNormalFileAttributes(targetFilePath);
                }
                CopyFile(file.FullName, targetFilePath, false, true, ct);              
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true, ct);
                }
            }
        }

        static internal void EnumerateDuplicates(string sourceDir, string destinationDir, ref List<string> duplicates)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();       

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);               
                if (File.Exists(targetFilePath))
                {
                    duplicates.Add(file.FullName);
                }
            }       
            
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                EnumerateDuplicates(subDir.FullName, newDestinationDir, ref duplicates);
            }            
        }
    }
}
