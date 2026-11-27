using Avalonia;
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
                        // The custom box reports the clicked button's name, or null when it was
                        // closed without a button (title-bar X, Alt+F4): treat that as the cancel button.
                        var clicked = await customWindow.ShowWindowDialogAsync(owner);
                        result = Enum.TryParse<ButtonResult>(clicked, out var parsed) ? parsed : GetCloseResult(button);
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
                    // Still answer: callers await this result, and skipping the callback left
                    // their command (e.g. F8) stuck disabled until restart.
                    Log.Error(ex, "MessageBox failed");
                    result = GetCloseResult(button);
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
            // Exactly one IsCancel button: with No and Cancel both marked, Esc pressed No, which
            // the overwrite / non-empty-delete callers read as "proceed without overwriting".
            var closeResult = GetCloseResult(button);
            return GetButtonResults(button).Select(r => new ButtonDefinition
            {
                Name = r.ToString(),
                IsDefault = r == defaultButton,
                IsCancel = r == closeResult
            }).ToArray();
        }

        private static ButtonResult[] GetButtonResults(ButtonEnum button)
        {
            return button switch
            {
                ButtonEnum.Ok => new[] { ButtonResult.Ok },
                ButtonEnum.YesNo => new[] { ButtonResult.Yes, ButtonResult.No },
                ButtonEnum.OkCancel => new[] { ButtonResult.Ok, ButtonResult.Cancel },
                ButtonEnum.OkAbort => new[] { ButtonResult.Ok, ButtonResult.Abort },
                ButtonEnum.YesNoCancel => new[] { ButtonResult.Yes, ButtonResult.No, ButtonResult.Cancel },
                ButtonEnum.YesNoAbort => new[] { ButtonResult.Yes, ButtonResult.No, ButtonResult.Abort },
                _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
            };
        }

        private static bool IsCancelResult(ButtonResult result)
        {
            return result is ButtonResult.No or ButtonResult.Abort or ButtonResult.Cancel;
        }

        // The most "back out" button of the set (Cancel/Abort over No; Ok for an Ok-only box).
        // Callers of YesNoCancel boxes test for Cancel and treat No as "proceed without
        // overwriting", so a box closed without a button must never come back as No there.
        internal static ButtonResult GetCloseResult(ButtonEnum button)
        {
            var results = GetButtonResults(button);
            return results.LastOrDefault(IsCancelResult, results[0]);
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
                            ContentTitle = messageBoxText,
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
                    // Answer as Cancel so the caller still runs its cleanup (New Folder's busy guard).
                    Log.Error(ex, "MessageBox input failed");
                    callbackArg = "";
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

            // The library's input row is a fixed 15px Grid row (ignores child margin), so push
            // the Auto-sized buttons row down instead to open up the gap.
            var buttonsPresenter = content.GetLogicalDescendants()
                .OfType<ItemsControl>()
                .FirstOrDefault(c => c.Name == "ButtonItemsPresenter");
            if (buttonsPresenter != null)
            {
                buttonsPresenter.Margin = new Thickness(0, 20, 0, 0);
            }
        }
    }
}
