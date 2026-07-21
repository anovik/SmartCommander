using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using Serilog;
using SmartCommander.Assets;
using System;
using System.Linq;
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
                                            object? parameter = null,
                                            ButtonResult? defaultButton = null)
        {
            this.resultAction = resultAction;
            this.resultInputAction = resultInputAction;
            this.messageBoxText = messageBoxText;
            this.caption = caption;
            this.button = button;
            this.icon = icon;
            this.parameter = parameter;
            this.defaultButton = defaultButton;
        }

        Action<ButtonResult, object?>? resultAction;
        Action<string>? resultInputAction;

        string messageBoxText;
        string caption;
        ButtonEnum button;
        Icon icon;
        object? parameter;
        ButtonResult? defaultButton;

        public void Show(Window owner)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ButtonResult result;
                try
                {
                    if (defaultButton.HasValue)
                    {
                        var customWindow = MsBox.Avalonia.MessageBoxManager
                            .GetMessageBoxCustom(new MessageBoxCustomParams()
                            {
                                ContentTitle = caption,
                                ContentMessage = messageBoxText + Environment.NewLine,
                                Icon = icon,
                                ButtonDefinitions = BuildButtonDefinitions(button, defaultButton.Value),
                                WindowStartupLocation = WindowStartupLocation.CenterScreen
                            });
                        var clicked = await customWindow.ShowWindowDialogAsync(owner);
                        result = Enum.Parse<ButtonResult>(clicked);
                    }
                    else
                    {
                        var messageBoxWindow = MsBox.Avalonia.MessageBoxManager
                            .GetMessageBoxStandard(caption, messageBoxText + Environment.NewLine, button, icon);
                        result = await messageBoxWindow.ShowWindowDialogAsync(owner);
                    }
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

        // Mirrors MsBoxStandardView.axaml's button set/order and IsCancel assignment per ButtonEnum.
        private static ButtonDefinition[] BuildButtonDefinitions(ButtonEnum button, ButtonResult defaultButton)
        {
            var results = button switch
            {
                ButtonEnum.Ok => new[] { ButtonResult.Ok },
                ButtonEnum.YesNo => new[] { ButtonResult.Yes, ButtonResult.No },
                ButtonEnum.OkCancel => new[] { ButtonResult.Ok, ButtonResult.Cancel },
                ButtonEnum.OkAbort => new[] { ButtonResult.Ok, ButtonResult.Abort },
                ButtonEnum.YesNoCancel => new[] { ButtonResult.Yes, ButtonResult.No, ButtonResult.Cancel },
                ButtonEnum.YesNoAbort => new[] { ButtonResult.Yes, ButtonResult.No, ButtonResult.Abort },
                _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
            };

            return results.Select(r => new ButtonDefinition
            {
                Name = r.ToString(),
                IsDefault = r == defaultButton,
                IsCancel = r is ButtonResult.No or ButtonResult.Abort or ButtonResult.Cancel
            }).ToArray();
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
                    var showTask = messageBoxWindow.ShowWindowDialogAsync(owner);
                    // MsBoxCustomView focuses its IsDefault button on load; steal it back at a lower priority so ours wins.
                    Dispatcher.UIThread.Post(() => FocusInputTextBox(owner), DispatcherPriority.Background);
                    var result = await showTask;
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

        private static void FocusInputTextBox(Window owner)
        {
            if (owner.OwnedWindows.LastOrDefault()?.Content is not Control content)
            {
                return;
            }

            var inputTextBox = content.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => !t.IsReadOnly);
            inputTextBox?.Focus();
        }
    }
}
