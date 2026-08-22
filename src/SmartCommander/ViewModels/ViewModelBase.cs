using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Views;
using System;
using System.Collections.Generic;

namespace SmartCommander.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        public event EventHandler<MvvmMessageBoxEventArgs>? MessageBoxRequest;
        public event EventHandler<MvvmMessageBoxEventArgs>? MessageBoxInputRequest;

        protected static TopLevel? GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                return TopLevel.GetTopLevel(desktopLifetime.MainWindow);
            }
            return null;
        }

        protected void MessageBox_Show(Action<ButtonResult, object?>? resultAction, string messageBoxText, string caption = "",
            ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None, object? parameter = null, ButtonResult? defaultButton = null)
        {
            if (this.MessageBoxRequest != null)
            {
                this.MessageBoxRequest(this, new MvvmMessageBoxEventArgs(resultAction, null, messageBoxText, caption,
                    button, icon, parameter, defaultButton));
            }
        }

        protected void MessageBoxInput_Show(Action<string>? resultAction, string messageBoxText, string caption = "")
        {
            if (this.MessageBoxInputRequest != null)
            {
                this.MessageBoxInputRequest(this, new MvvmMessageBoxEventArgs(null, resultAction, messageBoxText,
                    caption, ButtonEnum.OkCancel));
            }
        }

        // ex.Message alone is often just a wrapper like FluentFTP's "See InnerException for more
        // info." - walk the chain so the actual server/network reason reaches the message box.
        protected static string DescribeException(Exception ex)
        {
            var messages = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(e.Message) && !messages.Contains(e.Message))
                {
                    messages.Add(e.Message);
                }
            }
            return string.Join(" ", messages);
        }
    }
}
