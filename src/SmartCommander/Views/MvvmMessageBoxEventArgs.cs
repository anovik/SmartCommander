using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using Serilog;
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
                ButtonResult result;
                try
                {
                    var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                        .GetMessageBoxStandard(caption, messageBoxText + Environment.NewLine, button, icon);
                    result = await messageBoxWindow.ShowWindowDialogAsync(owner);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageBox failed");
                    return;
                }
                try
                {
                    resultAction?.Invoke(result, parameter);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageBox callback failed");
                }
            });
        }

        public void ShowInput(Window owner)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                string callbackArg;
                try
                {
                    var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                        .GetMessageBoxCustom(new MessageBoxCustomParams()
                        {
                            ContentHeader = messageBoxText,
                            ContentMessage = "",
                            MinWidth = 300,
                            InputParams = new InputParams() { },
                            ButtonDefinitions = new[] {
                                new ButtonDefinition {Name = Resources.OK, IsDefault = true},
                                new ButtonDefinition {Name = Resources.Cancel, IsCancel = true}
                            },
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        });
                    var result = await messageBoxWindow.ShowWindowDialogAsync(owner);
                    callbackArg = result == Resources.OK ? messageBoxWindow.InputValue : "";
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageBox input failed");
                    return;
                }
                try
                {
                    resultInputAction?.Invoke(callbackArg);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MessageBox input callback failed");
                }
            });
        }
    }
}
