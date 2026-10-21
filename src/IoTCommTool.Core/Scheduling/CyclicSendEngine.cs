using System.Diagnostics;

namespace IoTCommTool.Core.Scheduling;

/// <summary>循环发送引擎：按计划定时发送，配合 LossStatistics 做丢包测试</summary>
public sealed class CyclicSendEngine
{
    private static readonly TimeSpan s_oneSecond = TimeSpan.FromSeconds(1);

    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;

    /// <summary>是否正在运行</summary>
    public bool IsRunning => _isRunning;

    /// <summary>已发送次数</summary>
    public int SentCount { get; private set; }

    /// <summary>每发出一帧触发，参数为已发送次数</summary>
    public event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// 启动发送循环，返回的 Task 在计划完成、被取消或发送出错时结束；
    /// 同一时刻只允许一个计划运行
    /// </summary>
    public Task Start(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(send);
        ValidatePlan(plan);

        if (_isRunning)
            throw new InvalidOperationException("发送引擎正在运行");

        _cts = new CancellationTokenSource();
        _isRunning = true;
        SentCount = 0;

        return RunAsync(plan, send, stats, _cts.Token);
    }

    /// <summary>停止发送循环（Start 返回的 Task 随之完成）</summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, CancellationToken ct)
    {
        try
        {
            long startTimestamp = Stopwatch.GetTimestamp();

            switch (plan.Mode)
            {
                case SendMode.FixedInterval:
                    await RunFixedIntervalAsync(plan, send, stats, startTimestamp, ct).ConfigureAwait(false);
                    break;
                case SendMode.DurationEven:
                    await RunDurationEvenAsync(plan, send, stats, startTimestamp, ct).ConfigureAwait(false);
                    break;
                case SendMode.DurationRandom:
                    await RunDurationRandomAsync(plan, send, stats, startTimestamp, ct).ConfigureAwait(false);
                    break;
                case SendMode.PerSecondRandom:
                    await RunPerSecondRandomAsync(plan, send, stats, startTimestamp, ct).ConfigureAwait(false);
                    break;
                case SendMode.RandomInterval:
                    await RunRandomIntervalAsync(plan, send, stats, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task RunFixedIntervalAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, long startTimestamp, CancellationToken ct)
    {
        for (int i = 1; plan.Count == 0 || i <= plan.Count; i++)
        {
            await SendOnceAsync(i, plan, send, stats, ct).ConfigureAwait(false);
            await DelayUntilAsync(startTimestamp, plan.Interval * i, ct).ConfigureAwait(false);
        }
    }

    private async Task RunDurationEvenAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, long startTimestamp, CancellationToken ct)
    {
        TimeSpan interval = plan.Duration / plan.Count;
        for (int i = 1; i <= plan.Count; i++)
        {
            await SendOnceAsync(i, plan, send, stats, ct).ConfigureAwait(false);
            if (i < plan.Count)
                await DelayUntilAsync(startTimestamp, interval * i, ct).ConfigureAwait(false);
        }
    }

    private async Task RunDurationRandomAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, long startTimestamp, CancellationToken ct)
    {
        var offsets = new TimeSpan[plan.Count];
        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = plan.Duration * Random.Shared.NextDouble();
        Array.Sort(offsets);

        for (int i = 0; i < offsets.Length; i++)
        {
            await DelayUntilAsync(startTimestamp, offsets[i], ct).ConfigureAwait(false);
            await SendOnceAsync(i + 1, plan, send, stats, ct).ConfigureAwait(false);
        }
    }

    private async Task RunPerSecondRandomAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, long startTimestamp, CancellationToken ct)
    {
        int sent = 0;
        for (int window = 0; plan.Count == 0 || sent < plan.Count; window++)
        {
            var offsets = new TimeSpan[plan.PerSecondCount];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = s_oneSecond * Random.Shared.NextDouble();
            Array.Sort(offsets);

            foreach (TimeSpan offset in offsets)
            {
                if (plan.Count != 0 && sent >= plan.Count)
                    break;

                await DelayUntilAsync(startTimestamp, s_oneSecond * window + offset, ct).ConfigureAwait(false);
                await SendOnceAsync(++sent, plan, send, stats, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task RunRandomIntervalAsync(SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, CancellationToken ct)
    {
        for (int i = 1; plan.Count == 0 || i <= plan.Count; i++)
        {
            await SendOnceAsync(i, plan, send, stats, ct).ConfigureAwait(false);

            TimeSpan delay = plan.MinInterval + (plan.MaxInterval - plan.MinInterval) * Random.Shared.NextDouble();
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    private async Task SendOnceAsync(int index, SendPlan plan, Func<ReadOnlyMemory<byte>, CancellationToken, Task> send, LossStatistics? stats, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        ReadOnlyMemory<byte> payload = plan.PayloadFactory(index);
        await send(payload, ct).ConfigureAwait(false);

        SentCount = index;
        stats?.RecordSent((uint)index);
        ProgressChanged?.Invoke(this, index);
    }

    private static Task DelayUntilAsync(long startTimestamp, TimeSpan offset, CancellationToken ct)
    {
        long target = startTimestamp + (long)(offset.TotalSeconds * Stopwatch.Frequency);
        double remainingSeconds = (target - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
        return remainingSeconds <= 0 ? Task.CompletedTask : Task.Delay(TimeSpan.FromSeconds(remainingSeconds), ct);
    }

    private static void ValidatePlan(SendPlan plan)
    {
        if (plan.Count < 0)
            throw new ArgumentException("Count 不能为负数", nameof(plan));

        switch (plan.Mode)
        {
            case SendMode.FixedInterval:
                if (plan.Interval <= TimeSpan.Zero)
                    throw new ArgumentException("FixedInterval 模式下 Interval 必须大于 0", nameof(plan));
                break;
            case SendMode.DurationEven:
            case SendMode.DurationRandom:
                if (plan.Count <= 0)
                    throw new ArgumentException("Duration 类模式下 Count 必须大于 0", nameof(plan));
                if (plan.Duration <= TimeSpan.Zero)
                    throw new ArgumentException("Duration 必须大于 0", nameof(plan));
                break;
            case SendMode.PerSecondRandom:
                if (plan.PerSecondCount <= 0)
                    throw new ArgumentException("PerSecondRandom 模式下 PerSecondCount 必须大于 0", nameof(plan));
                break;
            case SendMode.RandomInterval:
                if (plan.MinInterval < TimeSpan.Zero || plan.MaxInterval < plan.MinInterval)
                    throw new ArgumentException("RandomInterval 模式下需要 0 <= MinInterval <= MaxInterval", nameof(plan));
                break;
        }
    }
}
