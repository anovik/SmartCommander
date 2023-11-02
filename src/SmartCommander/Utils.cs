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
    }
}
