using Avalonia.Controls;
using IoTCommTool.App.ViewModels;

namespace IoTCommTool.App.Views;

public partial class CyclicSendDialog : Window
{
    public CyclicSendDialog()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is RawConnectionViewModelBase vm && !string.IsNullOrWhiteSpace(vm.Title))
            Title = $"循环发送 - {vm.Title}";
    }
}
