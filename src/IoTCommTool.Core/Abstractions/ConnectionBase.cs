namespace IoTCommTool.Core.Abstractions;

/// <summary><see cref="IConnection"/> 的基类，提供状态与事件基础设施</summary>
public abstract class ConnectionBase : IConnection
{
    public Guid Id { get; } = Guid.NewGuid();

    public abstract ProtocolType Protocol { get; }

    /// <summary>显示名称，可由 UI 改名</summary>
    public string DisplayName { get; set; } = string.Empty;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<LogEventArgs>? Log;

    public abstract Task OpenAsync(CancellationToken ct = default);

    public abstract Task CloseAsync();

    /// <summary>默认不支持原始字节发送，原始协议（TCP/UDP）重写此方法</summary>
    public virtual Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => throw new NotSupportedException($"{Protocol} 协议不支持原始字节发送");

    /// <summary>状态变化时触发事件，重复状态不重复触发</summary>
    protected void SetState(ConnectionState state, string? reason = null)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, new StateChangedEventArgs(state, reason));
    }

    protected void RaiseLog(string message, LogLevel level = LogLevel.Info)
        => Log?.Invoke(this, new LogEventArgs(message, level));

    protected void RaiseDataReceived(byte[] data, string? remoteEndpoint = null)
        => DataReceived?.Invoke(this, new DataReceivedEventArgs(data, remoteEndpoint));

    public virtual async ValueTask DisposeAsync()
    {
        try
        {
            await CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // 释放时不抛出
        }

        GC.SuppressFinalize(this);
    }
}
