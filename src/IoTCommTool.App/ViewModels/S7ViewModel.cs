using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Common;
using IoTCommTool.Core.S7;
using IoTCommTool.Infrastructure;
using S7.Net;

namespace IoTCommTool.App.ViewModels;

public partial class S7ViewModel : PollingViewModelBase
{
    public S7ViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.S7;

    public IReadOnlyList<string> CpuTypes { get; } = new[] { "S7-300", "S7-400", "S7-1200", "S7-1500" };

    [ObservableProperty]
    public partial int CpuTypeIndex { get; set; } = 2;

    [ObservableProperty]
    public partial string Host { get; set; } = "192.168.0.1";

    [ObservableProperty]
    public partial string Port { get; set; } = "102";

    [ObservableProperty]
    public partial string Rack { get; set; } = "0";

    [ObservableProperty]
    public partial string Slot { get; set; } = "1";

    public IReadOnlyList<string> Areas { get; } = new[] { "DB", "M", "I", "Q" };

    [ObservableProperty]
    public partial int AreaIndex { get; set; }

    [ObservableProperty]
    public partial string DbNumber { get; set; } = "1";

    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0";

    [ObservableProperty]
    public partial string BitIndex { get; set; } = "0";

    public IReadOnlyList<string> DataTypes { get; } = new[] { "byte[]", "short", "int", "float", "bool", "string" };

    [ObservableProperty]
    public partial int DataTypeIndex { get; set; }

    [ObservableProperty]
    public partial string CountOrValue { get; set; } = "1";

    [ObservableProperty]
    public partial string ResultText { get; private set; } = string.Empty;

    public bool IsDbArea => AreaIndex == 0;

    public bool IsBoolType => DataTypeIndex == 4;

    public string CountOrValueLabel => DataTypeIndex switch
    {
        0 => "数量（字节）",
        4 => "值（true / false）",
        5 => "写入值 / 读取最大长度",
        _ => "值",
    };

    partial void OnCpuTypeIndexChanged(int value)
    {
        if (value <= 1)
        {
            Rack = "0";
            Slot = "2";
        }
        else
        {
            Rack = "0";
            Slot = "1";
        }

        RefreshAutoTitle();
    }

    partial void OnHostChanged(string value) => RefreshAutoTitle();

    partial void OnPortChanged(string value) => RefreshAutoTitle();

    partial void OnAreaIndexChanged(int value) => OnPropertyChanged(nameof(IsDbArea));

    partial void OnDataTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsBoolType));
        OnPropertyChanged(nameof(CountOrValueLabel));
    }

    [RelayCommand]
    private async Task ReadAsync()
    {
        if (Connection is not S7Connection s7)
        {
            AppendLog("未连接，无法读取", LogLevel.Warning);
            return;
        }

        try
        {
            SetResult(await ReadCurrentAsync(s7, CancellationToken.None));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog($"读取失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private async Task WriteAsync()
    {
        if (Connection is not S7Connection s7)
        {
            AppendLog("未连接，无法写入", LogLevel.Warning);
            return;
        }

        try
        {
            await WriteCurrentAsync(s7, CancellationToken.None);
            SetResult("写入成功");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog($"写入失败：{ex.Message}", LogLevel.Error);
        }
    }

    protected override async Task ExecutePollAsync(CancellationToken ct)
    {
        S7Connection s7 = Connection as S7Connection ?? throw new InvalidOperationException("未连接");
        SetResult(await ReadCurrentAsync(s7, ct));
    }

    protected override IConnection CreateConnection()
        => new S7Connection(
            MapCpu(CpuTypeIndex),
            InputParser.RequireHost(Host),
            InputParser.ParsePort(Port),
            (short)InputParser.ParseInt(Rack, 0, 7, "机架"),
            (short)InputParser.ParseInt(Slot, 0, 31, "插槽"));

    protected override string SerializeParams()
        => JsonSerializer.Serialize(new S7Params
        {
            Cpu = MapCpu(CpuTypeIndex).ToString(),
            Host = Host.Trim(),
            Port = InputParser.ParsePort(Port),
            Rack = InputParser.ParseInt(Rack, 0, 7, "机架"),
            Slot = InputParser.ParseInt(Slot, 0, 31, "插槽"),
        });

    public override void LoadParamsJson(string json)
    {
        try
        {
            S7Params? p = JsonSerializer.Deserialize<S7Params>(json);
            if (p is null)
                return;
            CpuTypeIndex = Enum.TryParse(p.Cpu, out CpuType cpu) ? MapCpuIndex(cpu) : 2;
            Host = p.Host;
            Port = p.Port.ToString();
            Rack = p.Rack.ToString();
            Slot = p.Slot.ToString();
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private async Task<string> ReadCurrentAsync(S7Connection s7, CancellationToken ct)
    {
        (DataType area, int db) = ResolveArea();
        int start = InputParser.ParseInt(StartAddress, 0, int.MaxValue, "起始地址");
        switch (DataTypeIndex)
        {
            case 0:
            {
                int count = InputParser.ParseInt(CountOrValue, 1, 65536, "数量");
                byte[] data = await s7.ReadBytesAsync(area, db, start, count, ct);
                return $"Hex：{ByteFormatter.ToHexString(data)}{Environment.NewLine}ASCII：{ByteFormatter.ToAsciiString(data)}";
            }
            case 1:
                return $"值：{await s7.ReadShortAsync(area, db, start, ct)}";
            case 2:
                return $"值：{await s7.ReadIntAsync(area, db, start, ct)}";
            case 3:
                return $"值：{await s7.ReadFloatAsync(area, db, start, ct)}";
            case 4:
            {
                int bit = InputParser.ParseInt(BitIndex, 0, 7, "位号");
                return $"值：{await s7.ReadBoolAsync(area, db, start, bit, ct)}";
            }
            default:
            {
                int maxLen = InputParser.ParseInt(CountOrValue, 1, 254, "最大长度");
                return $"值：\"{await s7.ReadStringAsync(area, db, start, maxLen, ct)}\"";
            }
        }
    }

    private async Task WriteCurrentAsync(S7Connection s7, CancellationToken ct)
    {
        (DataType area, int db) = ResolveArea();
        int start = InputParser.ParseInt(StartAddress, 0, int.MaxValue, "起始地址");
        switch (DataTypeIndex)
        {
            case 0:
                await s7.WriteBytesAsync(area, db, start, ByteFormatter.ParseHex(CountOrValue), ct);
                break;
            case 1:
                await s7.WriteShortAsync(area, db, start, InputParser.ParseShort(CountOrValue, "值"), ct);
                break;
            case 2:
                await s7.WriteIntAsync(area, db, start, InputParser.ParseInt(CountOrValue, int.MinValue, int.MaxValue, "值"), ct);
                break;
            case 3:
                await s7.WriteFloatAsync(area, db, start, InputParser.ParseFloat(CountOrValue, "值"), ct);
                break;
            case 4:
            {
                int bit = InputParser.ParseInt(BitIndex, 0, 7, "位号");
                await s7.WriteBoolAsync(area, db, start, bit, InputParser.ParseBool(CountOrValue, "值"), ct);
                break;
            }
            default:
                await s7.WriteStringAsync(area, db, start, CountOrValue.Length, CountOrValue, ct);
                break;
        }
    }

    private (DataType Area, int Db) ResolveArea()
    {
        DataType area = AreaIndex switch
        {
            0 => DataType.DataBlock,
            1 => DataType.Memory,
            2 => DataType.Input,
            _ => DataType.Output,
        };
        int db = AreaIndex == 0 ? InputParser.ParseInt(DbNumber, 1, 16000, "DB 号") : 0;
        return (area, db);
    }

    private void SetResult(string text) => Post(() => ResultText = text);

    private void RefreshAutoTitle() => UpdateAutoTitle($"{CpuTypes[CpuTypeIndex]} {Host}:{Port}");

    private static CpuType MapCpu(int index) => index switch
    {
        0 => CpuType.S7300,
        1 => CpuType.S7400,
        2 => CpuType.S71200,
        _ => CpuType.S71500,
    };

    private static int MapCpuIndex(CpuType cpu) => cpu switch
    {
        CpuType.S7300 => 0,
        CpuType.S7400 => 1,
        CpuType.S71200 => 2,
        _ => 3,
    };
}
