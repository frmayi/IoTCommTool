namespace IoTCommTool.Core.Scheduling;

/// <summary>循环发送模式</summary>
public enum SendMode
{
    /// <summary>固定间隔发送 N 次</summary>
    FixedInterval,

    /// <summary>在总时长内均匀发送 N 次</summary>
    DurationEven,

    /// <summary>在总时长内的随机时刻发送 N 次（预生成时刻表）</summary>
    DurationRandom,

    /// <summary>每 1 秒窗口内随机时刻发送 M 次</summary>
    PerSecondRandom,

    /// <summary>每次间隔在 [Min, Max] 内随机，发送 N 次</summary>
    RandomInterval
}
