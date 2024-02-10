using SmartCommander.Models;
using SmartCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SmartCommander
{
    static internal class Utils
    {
        static int oldProgressValue = 0;
        static internal void ReportProgress(IProgress<int>? progress, long processedSize, long totalSize)
        {
            if (progress == null)
            {
                return;
            }
            int newValue = totalSize == 0 ? 0 : Convert.ToInt32(processedSize * 100 / totalSize);
            if (newValue != oldProgressValue)
            {
                oldProgressValue = newValue;
                progress?.Report(newValue);
            }
        }
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

        static internal long GetTotalSize(List<FileViewModel> selectedItems)
        {          
            long totalSize = 0;
            foreach (var item in selectedItems)
            {
                string path = item.FullName;
                if (item.IsFolder)
                {
                    totalSize += GetDirectorySize(new DirectoryInfo(path));
                }
                else
                {
                    totalSize += new FileInfo(path).Length;
                }
            }
            return totalSize;
        }

        private static long GetDirectorySize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetDirectorySize(di);
            }
            return size;
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

        static internal void CopyFile(string source, 
                                      string dest,
                                      bool delete, 
                                      bool overwrite, 
                                      CancellationToken ct,
                                      IProgress<int>? progress, 
                                      ref long processedSize, 
                                      long totalSize)
        {
            long size = new FileInfo(source).Length;
            processedSize += size;
            if (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }

            if (!overwrite)
            {              
                if (File.Exists(dest))
                {
                    return;
                }
            }

            if (delete)
            {
                if (OperatingSystem.IsWindows())
                {
                    if (Path.GetPathRoot(source) == Path.GetPathRoot(dest))
                    {                      
                        File.Move(source, dest,overwrite);
                        return;
                    }                    
                }
            }

            const int bufferSize = 1024 * 1024; // 1MB
            const long limit = 10 * bufferSize;          

            if (size > limit)
            {
                using (Stream from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Write))
                using (Stream to = new FileStream(dest, FileMode.OpenOrCreate))
                {
                    // TODO: report progress by chunks
                    int readCount;
                    byte[] buffer = new byte[bufferSize];
                    while ((readCount = from.Read(buffer, 0, bufferSize)) != 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            ct.ThrowIfCancellationRequested();
                        }
                        to.Write(buffer, 0, readCount);
                    }
                }
            }
            else
            {
                File.Copy(source, dest, overwrite);
            }

            if (delete)
            {
                File.Delete(source);
            }
            Utils.ReportProgress(progress, processedSize, totalSize);
        }
      
        static internal void CopyDirectory(string sourceDir, 
                                           string destinationDir, 
                                           bool recursive, 
                                           bool overwrite, 
                                           CancellationToken ct,
                                           IProgress<int>? progress,
                                           ref long processedSize,
                                           long totalSize)
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
                CopyFile(file.FullName, targetFilePath, delete: false, overwrite, ct, 
                    progress, ref processedSize, totalSize);              
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
                    CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite, ct,
                        progress, ref processedSize, totalSize);
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
