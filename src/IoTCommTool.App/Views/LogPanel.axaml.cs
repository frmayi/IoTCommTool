using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using IoTCommTool.App.ViewModels;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.App.Views;

public partial class LogPanel : UserControl
{
    private INotifyCollectionChanged? _subscribed;

    public LogPanel()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Hook(DataContext as ConnectionViewModelBase);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Hook(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void Hook(ConnectionViewModelBase? vm)
    {
        if (_subscribed is not null)
            _subscribed.CollectionChanged -= OnLogLinesChanged;
        _subscribed = vm?.LogLines;
        if (_subscribed is not null)
            _subscribed.CollectionChanged += OnLogLinesChanged;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add
            && AutoScrollToggle.IsChecked == true
            && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items.Count - 1);
    }

    private void OnAutoScrollClick(object? sender, RoutedEventArgs e)
    {
        if (AutoScrollToggle.IsChecked == true && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items.Count - 1);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionViewModelBase vm || vm.LogLines.Count == 0)
            return;

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(BuildText(vm, includeHeader: false));
        }
        catch (Exception ex)
        {
            vm.AppendLog($"复制日志失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionViewModelBase vm)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        try
        {
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出通信日志",
                SuggestedFileName = BuildFileName(vm),
                DefaultExtension = "log",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("日志文件")
                    {
                        Patterns = new[] { "*.log", "*.txt" },
                    },
                },
            });

            if (file is null)
                return;

            await using Stream stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(BuildText(vm, includeHeader: true));
        }
        catch (Exception ex)
        {
            vm.AppendLog($"导出日志失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private static string BuildText(ConnectionViewModelBase vm, bool includeHeader)
    {
        var sb = new StringBuilder();
        if (includeHeader)
        {
            sb.AppendLine($"# IoTCommTool · {ProtocolItem.GetName(vm.Protocol)} · {vm.Title}");
            sb.AppendLine($"# 导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
        }

        foreach (string line in vm.LogLines)
            sb.AppendLine(line);
        return sb.ToString();
    }

    private static string BuildFileName(ConnectionViewModelBase vm)
    {
        string title = string.IsNullOrWhiteSpace(vm.Title) ? "log" : vm.Title;
        foreach (char c in Path.GetInvalidFileNameChars())
            title = title.Replace(c, '_');
        return $"IoTCommTool_{title}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
    }
}
