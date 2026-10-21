using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Common;
using IoTCommTool.Core.Scheduling;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public abstract partial class RawConnectionViewModelBase : ConnectionViewModelBase
{
    private readonly CyclicSendEngine _engine = new();
    private Task? _engineTask;
    private int _manualSeq;
    private bool _applyingHistory;
    private bool _applyingMac;
    private bool _registerHex = true;

    protected RawConnectionViewModelBase(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        Stats.Changed += (_, _) => Post(UpdateStatsText);
        _engine.ProgressChanged += (_, count) => Post(() => CycleProgressText = $"已发 {count} 次");
        foreach (LocalMacOption option in LocalMacAddressService.GetSnapshot())
            MacChoices.Add(option);
        UpdateStatsText();
        UpdateModeVisibility();
        UpdatePayloadInfo();
    }

    protected LossStatistics Stats { get; } = new();

    public override bool SupportsHexToggle => true;

    public IReadOnlyList<string> CycleModes { get; } = new[] { "固定间隔", "总时长均匀", "总时长随机", "每秒随机", "随机间隔" };

    public ObservableCollection<SendHistoryItem> SendHistory { get; } = new();

    public override bool DisplayHex
    {
        get => base.DisplayHex;
        set
        {
            if (base.DisplayHex == value)
                return;

            string previous = InputText;
            bool wasHex = base.DisplayHex;
            base.DisplayHex = value;

            if (!string.IsNullOrWhiteSpace(previous))
            {
                try
                {
                    InputText = value
                        ? ByteFormatter.ToHexString(ByteFormatter.FromAsciiEscaped(previous))
                        : ByteFormatter.ToAsciiEscaped(ByteFormatter.ParseHex(previous));
                }
                catch (FormatException)
                {
                    // Keep the raw text when it cannot be interpreted in the previous format.
                }
            }

            UpdatePayloadInfo();
        }
    }

    public bool RegisterHex
    {
        get => _registerHex;
        set
        {
            if (_registerHex == value)
                return;

            string previous = RegisterText;
            SetProperty(ref _registerHex, value);
            OnPropertyChanged(nameof(RegisterAscii));

            if (!string.IsNullOrWhiteSpace(previous))
            {
                try
                {
                    RegisterText = value
                        ? ByteFormatter.ToHexString(ByteFormatter.FromAsciiEscaped(previous))
                        : ByteFormatter.ToAsciiEscaped(ByteFormatter.ParseHex(previous));
                }
                catch (FormatException)
                {
                    // Keep the raw text when it cannot be interpreted in the previous format.
                }
            }
        }
    }

    public bool RegisterAscii
    {
        get => !RegisterHex;
        set => RegisterHex = !value;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AppendSequence { get; set; }

    [ObservableProperty]
    public partial bool AppendCrLf { get; set; }

    [ObservableProperty]
    public partial bool AppendCrc16 { get; set; }

    [ObservableProperty]
    public partial SendHistoryItem? SelectedHistory { get; set; }

    public virtual bool SupportsRegisterPacket => false;

    public ObservableCollection<LocalMacOption> MacChoices { get; } = new();

    [ObservableProperty]
    public partial bool RegisterEnabled { get; set; }

    [ObservableProperty]
    public partial string RegisterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial LocalMacOption? SelectedMacChoice { get; set; }

    [ObservableProperty]
    public partial string PayloadInfoText { get; private set; } = "等待输入报文";

    [ObservableProperty]
    public partial string StatsText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int CycleModeIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string IntervalMs { get; set; } = "1000";

    [ObservableProperty]
    public partial string CountText { get; set; } = "0";

    [ObservableProperty]
    public partial string DurationSec { get; set; } = "60";

    [ObservableProperty]
    public partial string PerSecondCountText { get; set; } = "5";

    [ObservableProperty]
    public partial string MinIntervalMs { get; set; } = "100";

    [ObservableProperty]
    public partial string MaxIntervalMs { get; set; } = "1000";

    [ObservableProperty]
    public partial bool CycleAppendSequence { get; set; } = true;

    [ObservableProperty]
    public partial bool IsCycling { get; private set; }

    [ObservableProperty]
    public partial string CycleProgressText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowCycleParams { get; private set; }

    [ObservableProperty]
    public partial bool ShowInterval { get; private set; }

    [ObservableProperty]
    public partial bool ShowDuration { get; private set; }

    [ObservableProperty]
    public partial bool ShowPerSecond { get; private set; }

    [ObservableProperty]
    public partial bool ShowMinMaxInterval { get; private set; }

    public bool CanStartCycle => IsConnected && !IsCycling;

    partial void OnCycleModeIndexChanged(int value) => UpdateModeVisibility();

    partial void OnIsCyclingChanged(bool value) => OnPropertyChanged(nameof(CanStartCycle));

    partial void OnInputTextChanged(string value) => UpdatePayloadInfo();

    partial void OnAppendSequenceChanged(bool value) => UpdatePayloadInfo();

    partial void OnAppendCrLfChanged(bool value) => UpdatePayloadInfo();

    partial void OnAppendCrc16Changed(bool value) => UpdatePayloadInfo();

    partial void OnSelectedHistoryChanged(SendHistoryItem? value)
    {
        if (_applyingHistory || value is null)
            return;

        _applyingHistory = true;
        InputText = value.InputText;
        DisplayHex = value.IsHex;
        AppendCrLf = value.AppendCrLf;
        AppendCrc16 = value.AppendCrc16;
        AppendSequence = value.AppendSequence;
        _applyingHistory = false;
        SelectedHistory = null;
    }

    partial void OnRegisterEnabledChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(RegisterText) && MacChoices.Count > 0)
            ApplyMacChoice(MacChoices[0]);
    }

    partial void OnSelectedMacChoiceChanged(LocalMacOption? value)
    {
        if (_applyingMac || value is null)
            return;

        ApplyMacChoice(value);
        _applyingMac = true;
        SelectedMacChoice = null;
        _applyingMac = false;
    }

    protected override void OnStatePresentationChanged()
    {
        base.OnStatePresentationChanged();
        OnPropertyChanged(nameof(CanStartCycle));
    }

    protected override void OnOpened()
    {
        base.OnOpened();
        if (SupportsRegisterPacket && RegisterEnabled)
            _ = SendRegisterAsync();
    }

    private void ApplyMacChoice(LocalMacOption option)
        => RegisterText = RegisterHex ? option.Mac.Replace(':', ' ') : option.Mac;

    private async Task SendRegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(RegisterText))
            return;

        byte[] payload;
        try
        {
            payload = RegisterHex
                ? ByteFormatter.ParseHex(RegisterText)
                : ByteFormatter.FromAsciiEscaped(RegisterText);
        }
        catch (FormatException ex)
        {
            AppendLog($"注册包解析失败，未发送：{ex.Message}", LogLevel.Warning);
            return;
        }

        if (payload.Length == 0)
            return;

        try
        {
            await SendPayloadAsync(payload, CancellationToken.None);
            string text = RegisterHex ? ByteFormatter.ToHexString(payload) : ByteFormatter.ToAsciiString(payload);
            AppendLog($"注册包已发送（{payload.Length} 字节）：{text}");
        }
        catch (Exception ex)
        {
            AppendLog($"注册包发送失败：{ex.Message}", LogLevel.Error);
        }
    }

    protected virtual Task SendPayloadAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => (Connection ?? throw new InvalidOperationException("未连接")).SendAsync(data, ct);

    protected string FormatData(byte[] data)
        => DisplayHex ? ByteFormatter.ToHexString(data) : ByteFormatter.ToAsciiString(data);

    protected override void OnDataReceived(DataReceivedEventArgs e)
    {
        uint? seq = null;
        if (AppendSequence || (IsCycling && CycleAppendSequence))
        {
            if (ByteFormatter.TryExtractSequence(e.Data, out uint parsed, out _))
                seq = parsed;
            Stats.RecordReceived(seq);
        }

        string remote = string.IsNullOrEmpty(e.RemoteEndpoint) ? string.Empty : $" {e.RemoteEndpoint}";
        string seqText = seq.HasValue ? $" 序号 {seq.Value}" : string.Empty;
        AppendLog($"接收{remote}（{e.Data.Length} 字节{seqText}）：{FormatData(e.Data)}");
    }

    [RelayCommand]
    private async Task SendManualAsync()
    {
        if (Connection is null)
        {
            AppendLog("未连接，无法发送", LogLevel.Warning);
            return;
        }

        if (!TryParsePayload(out byte[] payload))
            return;

        uint? sequence = null;
        if (AppendSequence)
        {
            sequence = (uint)Interlocked.Increment(ref _manualSeq);
            Stats.RecordSent(sequence.Value);
        }

        byte[] data = BuildFrame(payload, sequence ?? 0, AppendSequence);

        try
        {
            await SendPayloadAsync(data, CancellationToken.None);
            AppendLog($"发送（{data.Length} 字节）：{FormatData(data)}");
            AddHistory();
        }
        catch (Exception ex)
        {
            AppendLog($"发送失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private void StartCycle()
    {
        if (CycleModeIndex < 0)
        {
            AppendLog("请先选择循环发送模式", LogLevel.Warning);
            return;
        }

        if (Connection is null)
        {
            AppendLog("未连接，无法启动循环发送", LogLevel.Warning);
            return;
        }

        if (_engine.IsRunning || !TryParsePayload(out byte[] payload))
            return;

        SendPlan plan;
        try
        {
            plan = BuildPlan();
        }
        catch (FormatException ex)
        {
            AppendLog(ex.Message, LogLevel.Warning);
            return;
        }

        plan.PayloadFactory = i => BuildFrame(payload, (uint)i, CycleAppendSequence);
        Stats.Reset();

        Task engineTask;
        try
        {
            engineTask = _engine.Start(plan, SendPayloadAsync, Stats);
        }
        catch (Exception ex)
        {
            AppendLog($"循环发送启动失败：{ex.Message}", LogLevel.Error);
            return;
        }

        _engineTask = engineTask;
        IsCycling = true;
        CycleProgressText = "已发 0 次";
        AppendLog("循环发送已启动");
        _ = ObserveEngineAsync(engineTask);
    }

    [RelayCommand]
    private void StopCycle() => _engine.Stop();

    [RelayCommand]
    private void ResetStats()
    {
        Stats.Reset();
        UpdateStatsText();
    }

    public override async ValueTask DisposeAsync()
    {
        _engine.Stop();
        Task? task = _engineTask;
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

    private async Task ObserveEngineAsync(Task engineTask)
    {
        try
        {
            await engineTask;
            Post(() =>
            {
                IsCycling = false;
                AppendLog("循环发送已结束");
            });
        }
        catch (Exception ex)
        {
            Post(() =>
            {
                IsCycling = false;
                AppendLog($"循环发送终止：{ex.Message}", LogLevel.Error);
            });
        }
    }

    private byte[] BuildFrame(byte[] payload, uint sequence, bool appendSequence)
        => FrameComposer.Compose(
            payload,
            appendCrLf: AppendCrLf && !DisplayHex,
            appendSequence: appendSequence,
            sequence: sequence,
            appendCrc16: AppendCrc16 && DisplayHex);

    private void UpdatePayloadInfo()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            PayloadInfoText = "等待输入报文";
            return;
        }

        try
        {
            byte[] payload = DisplayHex ? ByteFormatter.ParseHex(InputText) : ByteFormatter.FromAsciiEscaped(InputText);
            int length = BuildFrame(payload, 0, AppendSequence).Length;
            PayloadInfoText = $"帧 {length} 字节";
        }
        catch (FormatException)
        {
            PayloadInfoText = "报文格式待校验";
        }
    }

    private void AddHistory()
    {
        string input = InputText;
        if (string.IsNullOrWhiteSpace(input))
            return;

        if (SendHistory.Count > 0)
        {
            SendHistoryItem first = SendHistory[0];
            if (first.InputText == input
                && first.IsHex == DisplayHex
                && first.AppendCrLf == AppendCrLf
                && first.AppendCrc16 == AppendCrc16
                && first.AppendSequence == AppendSequence)
                return;
        }

        SendHistory.Insert(0, new SendHistoryItem(input, DisplayHex, AppendCrLf, AppendCrc16, AppendSequence));
        while (SendHistory.Count > 20)
            SendHistory.RemoveAt(SendHistory.Count - 1);
    }

    private bool TryParsePayload(out byte[] data)
    {
        data = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(InputText))
        {
            AppendLog("请输入要发送的内容", LogLevel.Warning);
            return false;
        }

        try
        {
            data = DisplayHex ? ByteFormatter.ParseHex(InputText) : ByteFormatter.FromAsciiEscaped(InputText);
        }
        catch (FormatException ex)
        {
            AppendLog($"报文解析失败：{ex.Message}", LogLevel.Warning);
            return false;
        }

        if (data.Length == 0)
        {
            AppendLog("请输入要发送的内容", LogLevel.Warning);
            return false;
        }

        return true;
    }

    private SendPlan BuildPlan()
    {
        var plan = new SendPlan { Mode = (SendMode)CycleModeIndex };
        switch (plan.Mode)
        {
            case SendMode.FixedInterval:
                plan.Interval = TimeSpan.FromMilliseconds(InputParser.ParseInt(IntervalMs, 1, 3_600_000, "间隔(ms)"));
                plan.Count = InputParser.ParseInt(CountText, 0, int.MaxValue, "次数");
                break;
            case SendMode.DurationEven:
            case SendMode.DurationRandom:
                plan.Duration = TimeSpan.FromSeconds(InputParser.ParseInt(DurationSec, 1, 86400, "总时长(s)"));
                plan.Count = InputParser.ParseInt(CountText, 1, int.MaxValue, "次数");
                break;
            case SendMode.PerSecondRandom:
                plan.PerSecondCount = InputParser.ParseInt(PerSecondCountText, 1, 100_000, "每秒次数");
                plan.Count = InputParser.ParseInt(CountText, 0, int.MaxValue, "次数");
                break;
            case SendMode.RandomInterval:
                plan.MinInterval = TimeSpan.FromMilliseconds(InputParser.ParseInt(MinIntervalMs, 0, 3_600_000, "最小间隔(ms)"));
                plan.MaxInterval = TimeSpan.FromMilliseconds(InputParser.ParseInt(MaxIntervalMs, 0, 3_600_000, "最大间隔(ms)"));
                plan.Count = InputParser.ParseInt(CountText, 0, int.MaxValue, "次数");
                break;
        }

        return plan;
    }

    private void UpdateModeVisibility()
    {
        ShowCycleParams = CycleModeIndex >= 0;
        if (CycleModeIndex < 0)
        {
            ShowInterval = false;
            ShowDuration = false;
            ShowPerSecond = false;
            ShowMinMaxInterval = false;
            return;
        }

        var mode = (SendMode)CycleModeIndex;
        ShowInterval = mode == SendMode.FixedInterval;
        ShowDuration = mode is SendMode.DurationEven or SendMode.DurationRandom;
        ShowPerSecond = mode == SendMode.PerSecondRandom;
        ShowMinMaxInterval = mode == SendMode.RandomInterval;
    }

    private void UpdateStatsText()
        => StatsText = $"已发 {Stats.SentCount} · 已收 {Stats.ReceivedCount} · 丢失 {Stats.LostCount} · 乱序 {Stats.OutOfOrderCount} · 重复 {Stats.DuplicateCount} · 丢包率 {Stats.LossRate:P1}";
}
