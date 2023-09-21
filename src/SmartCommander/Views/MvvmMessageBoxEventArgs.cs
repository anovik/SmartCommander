using Avalonia.Controls;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public class MvvmMessageBoxEventArgs : EventArgs
    {        public MvvmMessageBoxEventArgs(Action<ButtonResult>? resultAction,
                                            Action<string>? resultInputAction,
                                            string messageBoxText, 
                                            string caption = "", 
                                            ButtonEnum button = ButtonEnum.Ok, 
                                            Icon icon = Icon.None)
        {
            this.resultAction = resultAction;
            this.resultInputAction = resultInputAction;
            this.messageBoxText = messageBoxText;
            this.caption = caption;
            this.button = button;
            this.icon = icon;       
        }

        Action<ButtonResult>? resultAction;
        Action<string>? resultInputAction;

        string messageBoxText;
        string caption;
        ButtonEnum button;
        Icon icon;   

        public async Task Show(Window owner)
        {
            var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(caption, messageBoxText + Environment.NewLine, button, icon);
            var  result = await messageBoxWindow.ShowAsPopupAsync(owner);
            resultAction?.Invoke(result);
        }

        public async Task ShowInput(Window owner)
        {
            var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxCustom(new MessageBoxCustomParams()
                {                 
                    ContentHeader = caption,
                    ContentMessage = messageBoxText,
                    MinWidth = 300,
                    InputParams = new InputParams() { },
                    ButtonDefinitions = new[] {
                        new ButtonDefinition {Name = "Ok"},
                        new ButtonDefinition {Name = "Cancel", IsCancel = true, IsDefault = true}
                    },
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                });
            var result = await messageBoxWindow.ShowAsPopupAsync(owner);          
            resultInputAction?.Invoke(result == "Ok" ? messageBoxWindow.InputValue : "");
        }
    }
}
