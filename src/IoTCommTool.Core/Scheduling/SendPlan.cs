namespace IoTCommTool.Core.Scheduling;

/// <summary>循环发送计划</summary>
public sealed class SendPlan
{
    /// <summary>发送模式</summary>
    public SendMode Mode { get; set; }

    /// <summary>固定间隔（FixedInterval 模式）</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>发送次数，0 表示不限（Duration 类模式必须大于 0）</summary>
    public int Count { get; set; } = 1;

    /// <summary>总时长（DurationEven / DurationRandom 模式）</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>每秒窗口内发送次数（PerSecondRandom 模式）</summary>
    public int PerSecondCount { get; set; } = 1;

    /// <summary>随机间隔下限（RandomInterval 模式）</summary>
    public TimeSpan MinInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>随机间隔上限（RandomInterval 模式）</summary>
    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>负载工厂，参数为第几次发送（从 1 开始），可用于附加递增序号</summary>
    public Func<int, ReadOnlyMemory<byte>> PayloadFactory { get; set; } = _ => ReadOnlyMemory<byte>.Empty;
}
