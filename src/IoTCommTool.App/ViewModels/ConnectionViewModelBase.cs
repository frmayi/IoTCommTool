using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Stopwatch = System.Diagnostics.Stopwatch;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public abstract partial class ConnectionViewModelBase : ViewModelBase, IAsyncDisposable
{
    private const int MaxLogLines = 2000;

    private readonly Action _savedConfigsChanged;
    private string _autoTitle = string.Empty;
    private bool _displayHex = true;
    private CancellationTokenSource? _connectCts;

    protected ConnectionViewModelBase(ConnectionConfigRepository repository, Action savedConfigsChanged)
    {
        Repository = repository;
        _savedConfigsChanged = savedConfigsChanged;
        ApplyState(ConnectionState.Disconnected, null);
    }

    protected ConnectionConfigRepository Repository { get; }

    public IConnection? Connection { get; private set; }

    public abstract ProtocolType Protocol { get; }

    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ConnectionState State { get; private set; }

    [ObservableProperty]
    public partial string StateText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IBrush StatusBrush { get; private set; } = Brushes.Gray;

    [ObservableProperty]
    public partial string ConnectButtonText { get; private set; } = "连接";

    [ObservableProperty]
    public partial int LogCount { get; private set; }

    public virtual bool DisplayHex
    {
        get => _displayHex;
        set
        {
            if (SetProperty(ref _displayHex, value))
                OnPropertyChanged(nameof(DisplayAscii));
        }
    }

    public bool DisplayAscii
    {
        get => !DisplayHex;
        set => DisplayHex = !value;
    }

    public virtual bool SupportsHexToggle => false;

    public bool IsConnected => State == ConnectionState.Connected;

    public bool IsFormEnabled => State is ConnectionState.Disconnected or ConnectionState.Faulted;

    public event EventHandler? CloseRequested;

    public event EventHandler? CloseTabsToLeftRequested;

    public event EventHandler? CloseTabsToRightRequested;

    public event EventHandler? CloseAllTabsRequested;

    protected virtual string OpenVerb => "连接";

    protected virtual string CloseVerb => "断开";

    protected virtual string ConnectedStateText => "已连接";

    protected abstract IConnection CreateConnection();

    protected abstract string SerializeParams();

    public abstract void LoadParamsJson(string json);

    protected virtual void OnOpened()
    {
    }

    protected virtual void OnClosed()
    {
    }

    protected virtual void OnStatePresentationChanged()
    {
    }

    protected virtual void OnDataReceived(DataReceivedEventArgs e)
    {
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (State == ConnectionState.Connecting)
        {
            try { _connectCts?.Cancel(); } catch (ObjectDisposedException) { }
            return;
        }

        if (Connection is not null)
            await CloseCoreAsync();
        else
            await OpenCoreAsync();
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
        LogCount = 0;
    }

    [RelayCommand]
    private void CloseRequest() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CloseTabsToLeft() => CloseTabsToLeftRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CloseTabsToRight() => CloseTabsToRightRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CloseAllTabs() => CloseAllTabsRequested?.Invoke(this, EventArgs.Empty);

    public void AppendLog(string message, LogLevel level = LogLevel.Info)
    {
        Post(() =>
        {
            string tag = level switch
            {
                LogLevel.Warning => "[警告] ",
                LogLevel.Error => "[错误] ",
                _ => string.Empty,
            };
            LogLines.Add($"[{DateTime.Now:HH:mm:ss.fff}] {tag}{message}");
            while (LogLines.Count > MaxLogLines)
                LogLines.RemoveAt(0);
            LogCount = LogLines.Count;
        });
    }

    protected void UpdateAutoTitle(string candidate)
    {
        if (string.IsNullOrEmpty(Title) || Title == _autoTitle)
            Title = candidate;
        _autoTitle = candidate;
    }

    protected static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public virtual async ValueTask DisposeAsync()
    {
        try { _connectCts?.Cancel(); } catch (ObjectDisposedException) { }

        IConnection? connection = Connection;
        Connection = null;
        if (connection is not null)
        {
            Unhook(connection);
            await DisposeQuietlyAsync(connection);
        }
    }

    private async Task OpenCoreAsync()
    {
        IConnection connection;
        try
        {
            connection = CreateConnection();
        }
        catch (Exception ex)
        {
            AppendLog($"参数错误：{ex.Message}", LogLevel.Error);
            return;
        }

        connection.StateChanged += OnConnectionStateChanged;
        connection.Log += OnConnectionLog;
        connection.DataReceived += OnConnectionDataReceived;
        Connection = connection;

        var stopwatch = Stopwatch.StartNew();
        var cts = new CancellationTokenSource();
        _connectCts = cts;

        try
        {
            await connection.OpenAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            stopwatch.Stop();
            AppendLog($"已取消{OpenVerb}");
            CleanupConnection(connection);
            ApplyState(ConnectionState.Disconnected, "已取消");
            return;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AppendLog($"{OpenVerb}失败（耗时 {stopwatch.ElapsedMilliseconds} ms）：{ex.Message}", LogLevel.Error);
            CleanupConnection(connection);
            return;
        }
        finally
        {
            if (ReferenceEquals(_connectCts, cts))
                _connectCts = null;
            cts.Dispose();
        }

        stopwatch.Stop();
        AppendLog($"{OpenVerb}成功（耗时 {stopwatch.ElapsedMilliseconds} ms）");

        if (string.IsNullOrWhiteSpace(Title))
            Title = connection.DisplayName;
        else if (connection is ConnectionBase connectionBase)
            connectionBase.DisplayName = Title;

        OnOpened();
        await SaveConfigAsync();
    }

    private async Task CloseCoreAsync()
    {
        IConnection? connection = Connection;
        if (connection is null)
            return;

        Connection = null;
        Unhook(connection);
        try
        {
            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"{CloseVerb}异常：{ex.Message}", LogLevel.Warning);
        }

        OnClosed();
        await DisposeQuietlyAsync(connection);
        ApplyState(ConnectionState.Disconnected, null);
    }

    private void CleanupConnection(IConnection connection)
    {
        if (ReferenceEquals(Connection, connection))
            Connection = null;
        Unhook(connection);
        _ = DisposeQuietlyAsync(connection);
    }

    private void Unhook(IConnection connection)
    {
        connection.StateChanged -= OnConnectionStateChanged;
        connection.Log -= OnConnectionLog;
        connection.DataReceived -= OnConnectionDataReceived;
    }

    private static async Task DisposeQuietlyAsync(IConnection connection)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
        }
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            await Repository.UpsertAsync(Protocol, Title, SerializeParams());
            _savedConfigsChanged();
        }
        catch (Exception ex)
        {
            AppendLog($"保存配置失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private void OnConnectionStateChanged(object? sender, StateChangedEventArgs e)
    {
        Post(() =>
        {
            ApplyState(e.State, e.Reason);
            if (e.State is ConnectionState.Disconnected or ConnectionState.Faulted
                && sender is IConnection connection
                && ReferenceEquals(Connection, connection))
            {
                CleanupConnection(connection);
                OnClosed();
            }
        });
    }

    private void OnConnectionLog(object? sender, LogEventArgs e) => AppendLog(e.Message, e.Level);

    private void OnConnectionDataReceived(object? sender, DataReceivedEventArgs e) => OnDataReceived(e);

    private void ApplyState(ConnectionState state, string? reason)
    {
        State = state;
        StateText = state switch
        {
            ConnectionState.Connecting => "连接中…",
            ConnectionState.Connected => ConnectedStateText,
            ConnectionState.Faulted => string.IsNullOrEmpty(reason) ? "故障" : $"故障：{reason}",
            _ => string.IsNullOrEmpty(reason) ? "未连接" : $"未连接（{reason}）",
        };
        StatusBrush = state switch
        {
            ConnectionState.Connecting => Brushes.Orange,
            ConnectionState.Connected => Brushes.ForestGreen,
            ConnectionState.Faulted => Brushes.IndianRed,
            _ => Brushes.Gray,
        };
        ConnectButtonText = state switch
        {
            ConnectionState.Connecting => "取消",
            ConnectionState.Connected => CloseVerb,
            _ => OpenVerb,
        };
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsFormEnabled));
        OnStatePresentationChanged();
    }
}
