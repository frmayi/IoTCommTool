using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Scheduling;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public abstract partial class PollingViewModelBase : ConnectionViewModelBase
{
    private readonly CyclicSendEngine _pollEngine = new();
    private Task? _pollTask;
    private int _pollOk;
    private int _pollFail;

    protected PollingViewModelBase(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
    }

    [ObservableProperty]
    public partial string PollIntervalMs { get; set; } = "1000";

    [ObservableProperty]
    public partial bool IsPolling { get; private set; }

    [ObservableProperty]
    public partial string PollStatsText { get; private set; } = "成功 0   失败 0";

    public bool CanStartPoll => IsConnected && !IsPolling;

    partial void OnIsPollingChanged(bool value) => OnPropertyChanged(nameof(CanStartPoll));

    protected override void OnStatePresentationChanged()
    {
        base.OnStatePresentationChanged();
        OnPropertyChanged(nameof(CanStartPoll));
    }

    [RelayCommand]
    private void StartPoll()
    {
        if (Connection is null)
        {
            AppendLog("未连接，无法启动轮询", LogLevel.Warning);
            return;
        }

        if (_pollEngine.IsRunning)
            return;

        if (!CanPoll(out string reason))
        {
            AppendLog(reason, LogLevel.Warning);
            return;
        }

        SendPlan plan;
        try
        {
            plan = new SendPlan
            {
                Mode = SendMode.FixedInterval,
                Interval = TimeSpan.FromMilliseconds(InputParser.ParseInt(PollIntervalMs, 10, 3_600_000, "轮询间隔(ms)")),
                Count = 0,
                PayloadFactory = static _ => ReadOnlyMemory<byte>.Empty,
            };
        }
        catch (FormatException ex)
        {
            AppendLog(ex.Message, LogLevel.Warning);
            return;
        }

        Task task;
        try
        {
            task = _pollEngine.Start(plan, PollOnceAsync, null);
        }
        catch (Exception ex)
        {
            AppendLog($"轮询启动失败：{ex.Message}", LogLevel.Error);
            return;
        }

        _pollTask = task;
        _pollOk = 0;
        _pollFail = 0;
        UpdatePollStatsText();
        IsPolling = true;
        AppendLog("轮询已启动");
        _ = ObservePollAsync(task);
    }

    [RelayCommand]
    private void StopPoll() => _pollEngine.Stop();

    protected virtual bool CanPoll(out string reason)
    {
        reason = string.Empty;
        return true;
    }

    protected abstract Task ExecutePollAsync(CancellationToken ct);

    public override async ValueTask DisposeAsync()
    {
        _pollEngine.Stop();
        Task? task = _pollTask;
        if (task is not null)
        {
            try
            {
                await task;
            }
            catch
            {
            }
        }

        await base.DisposeAsync();
    }

    private async Task PollOnceAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        try
        {
            await ExecutePollAsync(ct);
            Post(() =>
            {
                _pollOk++;
                UpdatePollStatsText();
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Post(() =>
            {
                _pollFail++;
                UpdatePollStatsText();
                AppendLog($"轮询失败：{ex.Message}", LogLevel.Warning);
            });
        }
    }

    private async Task ObservePollAsync(Task pollTask)
    {
        try
        {
            await pollTask;
        }
        catch (Exception ex)
        {
            Post(() => AppendLog($"轮询终止：{ex.Message}", LogLevel.Error));
        }
        finally
        {
            Post(() =>
            {
                IsPolling = false;
                AppendLog("轮询已停止");
            });
        }
    }

    private void UpdatePollStatsText() => PollStatsText = $"成功 {_pollOk}   失败 {_pollFail}";
}
