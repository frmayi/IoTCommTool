namespace IoTCommTool.Core.Abstractions;

/// <summary>收到原始数据时的事件参数</summary>
public sealed class DataReceivedEventArgs : EventArgs
{
    public DataReceivedEventArgs(byte[] data, string? remoteEndpoint)
    {
        Data = data;
        RemoteEndpoint = remoteEndpoint;
        Timestamp = DateTime.Now;
    }

    /// <summary>收到的数据</summary>
    public byte[] Data { get; }

    /// <summary>对端地址（如 192.168.1.10:5000），无对端概念时为 null</summary>
    public string? RemoteEndpoint { get; }

    /// <summary>接收时间（本地时间）</summary>
    public DateTime Timestamp { get; }
}

/// <summary>日志级别</summary>
public enum LogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>日志事件参数</summary>
public sealed class LogEventArgs : EventArgs
{
    public LogEventArgs(string message, LogLevel level)
    {
        Message = message;
        Level = level;
        Timestamp = DateTime.Now;
    }

    /// <summary>日志内容</summary>
    public string Message { get; }

    /// <summary>日志级别</summary>
    public LogLevel Level { get; }

    /// <summary>日志时间（本地时间）</summary>
    public DateTime Timestamp { get; }
}

/// <summary>连接状态变化事件参数</summary>
public sealed class StateChangedEventArgs : EventArgs
{
    public StateChangedEventArgs(ConnectionState state, string? reason)
    {
        State = state;
        Reason = reason;
    }

    /// <summary>新状态</summary>
    public ConnectionState State { get; }

    /// <summary>状态变化原因，可为 null</summary>
    public string? Reason { get; }
}
