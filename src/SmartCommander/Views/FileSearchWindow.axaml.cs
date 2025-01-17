using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SmartCommander;

public partial class FileSearchWindow : Window
{  
    public FileSearchWindow()
    {
        InitializeComponent();

        var listBox = this.FindControl<ListBox>("SearchListBox");
        listBox!.AddHandler(KeyDownEvent, (sender, e) =>
        {
            if (e.Key == Key.Enter)
            {
                HandleItemAction((string)listBox.SelectedValue!);
                Close();
            }
        }, RoutingStrategies.Tunnel);

        listBox!.DoubleTapped += (sender, e) =>
        {
            HandleItemAction((string)listBox.SelectedValue!);
            Close();
        };
    }

    private void HandleItemAction(string? filename)
    {
        var viewModel=DataContext as FileSearchViewModel;
        viewModel!.ResultFilename = filename?? string.Empty;
    }
}