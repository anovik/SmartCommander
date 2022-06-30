using MessageBox.Avalonia.Enums;
using ReactiveUI;
using SmartCommander.Views;
using System;

namespace SmartCommander.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        public event EventHandler<MvvmMessageBoxEventArgs> MessageBoxRequest;
        protected void MessageBox_Show(Action<ButtonResult> resultAction, string messageBoxText, string caption = "",
            ButtonEnum button = ButtonEnum.Ok, Icon icon = Icon.None)
        {
            if (this.MessageBoxRequest != null)
            {
                this.MessageBoxRequest(this, new MvvmMessageBoxEventArgs(resultAction, messageBoxText, caption, button, icon));
            }
        }
    }
}
