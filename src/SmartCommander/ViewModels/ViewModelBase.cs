using MsBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Views;
using System;

namespace SmartCommander.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        public event EventHandler<MvvmMessageBoxEventArgs>? MessageBoxRequest;
        public event EventHandler<MvvmMessageBoxEventArgs>? MessageBoxInputRequest;
        public event EventHandler<int>? ProgressRequest;

        protected void Progress_Show(int value)
        {
            if (this.ProgressRequest != null)
            {
                this.ProgressRequest(this, value);
            }
        }

        protected void MessageBox_Show(Action<ButtonResult, object?>? resultAction, string messageBoxText, string caption = "",
            ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None, object? parameter = null)
        {
            if (this.MessageBoxRequest != null)
            {
                this.MessageBoxRequest(this, new MvvmMessageBoxEventArgs(resultAction, null, messageBoxText, caption, 
                    button, icon, parameter));
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
    }
}
