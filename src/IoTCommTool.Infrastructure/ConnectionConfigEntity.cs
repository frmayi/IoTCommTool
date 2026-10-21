namespace IoTCommTool.Infrastructure;

/// <summary>连接配置记录</summary>
public sealed class ConnectionConfigEntity
{
    /// <summary>自增主键</summary>
    public long Id { get; set; }

    /// <summary>协议类型（ProtocolType 枚举名）</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>连接参数 JSON</summary>
    public string ParamsJson { get; set; } = string.Empty;

    /// <summary>最近使用时间（ISO8601 本地时间字符串）</summary>
    public string LastUsedAt { get; set; } = string.Empty;
}
