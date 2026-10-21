namespace IoTCommTool.Core.Abstractions;

/// <summary>连接状态</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted
}
