using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using SmartCommander.Assets;
using System;
using System.Threading.Tasks;

namespace SmartCommander.Views
{
    public class MvvmMessageBoxEventArgs : EventArgs
    {        public MvvmMessageBoxEventArgs(Action<ButtonResult, object?>? resultAction,
                                            Action<string>? resultInputAction,
                                            string messageBoxText, 
                                            string caption = "", 
                                            ButtonEnum button = ButtonEnum.Ok, 
                                            Icon icon = Icon.None,
                                            object? parameter = null)
        {
            this.resultAction = resultAction;
            this.resultInputAction = resultInputAction;
            this.messageBoxText = messageBoxText;
            this.caption = caption;
            this.button = button;
            this.icon = icon;
            this.parameter = parameter;
        }

        Action<ButtonResult, object?>? resultAction;
        Action<string>? resultInputAction;

        string messageBoxText;
        string caption;
        ButtonEnum button;
        Icon icon;
        object? parameter;

        public void Show(Window owner)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(caption, messageBoxText + Environment.NewLine, button, icon);
                var result = await messageBoxWindow.ShowWindowDialogAsync(owner);
                resultAction?.Invoke(result, parameter);
            });
        }

        public void ShowInput(Window owner)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxCustom(new MessageBoxCustomParams()
                {
                    ContentHeader = caption,
                    ContentMessage = messageBoxText,
                    MinWidth = 300,
                    InputParams = new InputParams() { },
                    ButtonDefinitions = new[] {
                        new ButtonDefinition {Name = Resources.OK},
                        new ButtonDefinition {Name = Resources.Cancel, IsCancel = true, IsDefault = true}
                    },
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                });
                var result = await messageBoxWindow.ShowWindowDialogAsync(owner);
                resultInputAction?.Invoke(result == Resources.OK ? messageBoxWindow.InputValue : "");
            });
        }
    }
}
