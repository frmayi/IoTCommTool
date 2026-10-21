using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using IoTCommTool.App.ViewModels;

namespace IoTCommTool.App.Views;

public partial class SendPanel : UserControl
{
    private CyclicSendDialog? _cyclicSendDialog;
    private AsciiReferenceDialog? _asciiDialog;

    public SendPanel()
    {
        InitializeComponent();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RawConnectionViewModelBase vm)
            return;

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.SendManualCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnOpenCyclicSendClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RawConnectionViewModelBase vm)
            return;

        if (_cyclicSendDialog is not null)
        {
            _cyclicSendDialog.Activate();
            return;
        }

        var dialog = new CyclicSendDialog { DataContext = vm };
        dialog.Closed += (_, _) => _cyclicSendDialog = null;
        _cyclicSendDialog = dialog;
        if (TopLevel.GetTopLevel(this) is Window owner)
            dialog.Show(owner);
        else
            dialog.Show();
    }

    private void OnOpenAsciiTableClick(object? sender, RoutedEventArgs e)
    {
        if (_asciiDialog is not null)
        {
            _asciiDialog.Activate();
            return;
        }

        var dialog = new AsciiReferenceDialog();
        dialog.Closed += (_, _) => _asciiDialog = null;
        _asciiDialog = dialog;
        if (TopLevel.GetTopLevel(this) is Window owner)
            dialog.Show(owner);
        else
            dialog.Show();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _cyclicSendDialog?.Close();
        _cyclicSendDialog = null;
        _asciiDialog?.Close();
        _asciiDialog = null;
        base.OnDetachedFromVisualTree(e);
    }
}
