using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SmartCommander.Assets;
using SmartCommander.ViewModels;
using System;

namespace SmartCommander
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {      
            var name = data!.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                return new TextBlock { Text = Resources.NotFound + name };
            }
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
