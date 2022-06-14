using System;

namespace SmartCommander.ViewModels
{
    public class FileViewModel
    {
        public FileViewModel()
        {

        }
        public string Name { get; set; }
        public string Extension { get; set; }
        public string Size { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
