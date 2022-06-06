using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCommander.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string CurrentDirectory1 { get; set; }

        public string CurrentDirectory2 { get; set; }

        public MainWindowViewModel()
        {
            CurrentDirectory1 = CurrentDirectory2
                = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }
    }
}
