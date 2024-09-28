using Avalonia.Controls;
using System;
using System.IO;

namespace SmartCommander.ViewModels
{
    public class ViewerViewModel : ViewModelBase
    {

        public ViewerViewModel(string filename)
        {
            try
            {
                Text = File.ReadAllText(filename);
            }
            catch { Text = ""; }
            Filename= filename;
        }

        public string Text { get; set; }
        public string Filename { get; set; }
    }
}
