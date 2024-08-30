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
        }

        public string Text { get; set; }
    }
}
