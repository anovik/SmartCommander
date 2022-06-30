using Avalonia.Controls;
using MessageBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public class MvvmMessageBoxEventArgs : EventArgs
    {        public MvvmMessageBoxEventArgs(Action<ButtonResult> resultAction, string messageBoxText, string caption = "", 
                                            ButtonEnum button = ButtonEnum.Ok, Icon icon =Icon.None)
        {
            this.resultAction = resultAction;
            this.messageBoxText = messageBoxText;
            this.caption = caption;
            this.button = button;
            this.icon = icon;       
        }

        Action<ButtonResult> resultAction;
        string messageBoxText;
        string caption;
        ButtonEnum button;
        Icon icon;   

        public async Task Show(Window owner)
        {
            var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandardWindow(caption, messageBoxText + Environment.NewLine, button, icon);
            ButtonResult  result = await messageBoxStandardWindow.ShowDialog(owner);
            if (resultAction != null) resultAction(result);
        }

        public async Task Show()
        {
            var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
               .GetMessageBoxStandardWindow(caption, messageBoxText + Environment.NewLine, button, icon);
            ButtonResult result = await messageBoxStandardWindow.Show();
            if (resultAction != null) resultAction(result);
        }
    }
}
