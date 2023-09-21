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

        protected void MessageBox_Show(Action<ButtonResult>? resultAction, string messageBoxText, string caption = "",
            ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None)
        {
            if (this.MessageBoxRequest != null)
            {
                this.MessageBoxRequest(this, new MvvmMessageBoxEventArgs(resultAction, null, messageBoxText, caption, 
                    button, icon));
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
