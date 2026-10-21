namespace IoTCommTool.Core.Abstractions;

/// <summary>通信连接的统一抽象，事件驱动、全异步</summary>
public interface IConnection : IAsyncDisposable
{
    /// <summary>连接实例唯一标识</summary>
    Guid Id { get; }

    /// <summary>协议类型</summary>
    ProtocolType Protocol { get; }

    /// <summary>显示名称</summary>
    string DisplayName { get; }

    /// <summary>当前连接状态</summary>
    ConnectionState State { get; }

    /// <summary>状态变化时触发</summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>收到原始字节数据时触发（Modbus/S7 类协议不触发）</summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>产生运行日志时触发</summary>
    event EventHandler<LogEventArgs>? Log;

    /// <summary>打开连接</summary>
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>关闭连接</summary>
    Task CloseAsync();

    /// <summary>发送原始字节；Modbus/S7 类协议抛出 <see cref="NotSupportedException"/></summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}
