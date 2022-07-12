using Avalonia.Controls;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public class MvvmMessageBoxEventArgs : EventArgs
    {        public MvvmMessageBoxEventArgs(Action<ButtonResult>? resultAction,
                                            Action<MessageWindowResultDTO>? resultInputAction,
                                            string messageBoxText, string caption = "", 
                                            ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None)
        {
            this.resultAction = resultAction;
            this.resultInputAction = resultInputAction;
            this.messageBoxText = messageBoxText;
            this.caption = caption;
            this.button = button;
            this.icon = icon;       
        }

        Action<ButtonResult>? resultAction;
        Action<MessageWindowResultDTO>? resultInputAction;

        string messageBoxText;
        string caption;
        ButtonEnum button;
        Icon icon;   

        public async Task Show(Window owner)
        {
            var messageBoxWindow = MessageBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandardWindow(caption, messageBoxText + Environment.NewLine, button, icon);
            var  result = await messageBoxWindow.ShowDialog(owner);
            resultAction?.Invoke(result);
        }

        public async Task ShowInput(Window owner)
        {
            var messageBoxWindow = MessageBox.Avalonia.MessageBoxManager
                .GetMessageBoxInputWindow(new MessageBoxInputParams() 
                    { ContentTitle = caption, ContentMessage = messageBoxText, MinWidth = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner });
            var result = await messageBoxWindow.ShowDialog(owner);
            resultInputAction?.Invoke(result);
        }   
    }
}
