using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public abstract partial class ModbusMasterViewModelBase : PollingViewModelBase
{
    protected ModbusMasterViewModelBase(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
    }

    public IReadOnlyList<string> FunctionCodes { get; } = new[]
    {
        "01 读线圈", "02 读离散输入", "03 读保持寄存器", "04 读输入寄存器",
        "05 写单线圈", "06 写单寄存器", "0F 写多线圈", "10 写多寄存器",
    };

    [ObservableProperty]
    public partial int FunctionCodeIndex { get; set; } = 2;

    public bool IsWriteFunction => FunctionCodeIndex >= 4;

    public string ExecuteButtonText => IsWriteFunction ? "执行写入" : "执行读取";

    partial void OnFunctionCodeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsWriteFunction));
        OnPropertyChanged(nameof(ExecuteButtonText));
    }

    [ObservableProperty]
    public partial string UnitId { get; set; } = "1";

    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0";

    [ObservableProperty]
    public partial string Count { get; set; } = "10";

    [ObservableProperty]
    public partial string ValuesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultText { get; private set; } = string.Empty;

    protected ModbusMasterConnectionBase? Master => Connection as ModbusMasterConnectionBase;

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        ModbusMasterConnectionBase? master = Master;
        if (master is null)
        {
            AppendLog("未连接，无法执行", LogLevel.Warning);
            return;
        }

        try
        {
            await ExecuteFunctionAsync(master, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog($"执行失败：{ex.Message}", LogLevel.Error);
        }
    }

    protected override bool CanPoll(out string reason)
    {
        if (FunctionCodeIndex > 3)
        {
            reason = "写功能码不支持轮询，请选择 01~04 读功能码";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    protected override async Task ExecutePollAsync(CancellationToken ct)
    {
        ModbusMasterConnectionBase master = Master ?? throw new InvalidOperationException("未连接");
        await ExecuteFunctionAsync(master, ct);
    }

    private async Task ExecuteFunctionAsync(ModbusMasterConnectionBase master, CancellationToken ct)
    {
        byte unitId = InputParser.ParseByte(UnitId, "站号");
        int start = InputParser.ParseInt(StartAddress, 0, 65535, "起始地址");

        switch (FunctionCodeIndex)
        {
            case 0:
            {
                bool[] values = await master.ReadCoilsAsync(unitId, start, InputParser.ParseInt(Count, 1, 2000, "数量"), ct);
                SetResult(FormatBools("线圈", start, values));
                break;
            }
            case 1:
            {
                bool[] values = await master.ReadDiscreteInputsAsync(unitId, start, InputParser.ParseInt(Count, 1, 2000, "数量"), ct);
                SetResult(FormatBools("离散输入", start, values));
                break;
            }
            case 2:
            {
                short[] values = await master.ReadHoldingRegistersAsync(unitId, start, InputParser.ParseInt(Count, 1, 125, "数量"), ct);
                SetResult(FormatRegisters("保持寄存器", start, values));
                break;
            }
            case 3:
            {
                short[] values = await master.ReadInputRegistersAsync(unitId, start, InputParser.ParseInt(Count, 1, 125, "数量"), ct);
                SetResult(FormatRegisters("输入寄存器", start, values));
                break;
            }
            case 4:
            {
                bool value = InputParser.ParseBool(ValuesText, "线圈值");
                await master.WriteSingleCoilAsync(unitId, start, value, ct);
                SetResult($"线圈[{start}] 写入成功：{value}");
                break;
            }
            case 5:
            {
                short value = InputParser.ParseShort(ValuesText, "寄存器值");
                await master.WriteSingleRegisterAsync(unitId, start, value, ct);
                SetResult($"保持寄存器[{start}] 写入成功：{value}（0x{value & 0xFFFF:X4}）");
                break;
            }
            case 6:
            {
                bool[] values = InputParser.ParseBoolList(ValuesText, "线圈值");
                await master.WriteMultipleCoilsAsync(unitId, start, values, ct);
                SetResult($"线圈[{start}..{start + values.Length - 1}] 写入 {values.Length} 个成功");
                break;
            }
            case 7:
            {
                short[] values = InputParser.ParseShortList(ValuesText, "寄存器值");
                await master.WriteMultipleRegistersAsync(unitId, start, values, ct);
                SetResult($"保持寄存器[{start}..{start + values.Length - 1}] 写入 {values.Length} 个成功");
                break;
            }
        }
    }

    private void SetResult(string text) => Post(() => ResultText = text);

    private static string FormatRegisters(string name, int start, short[] values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
            sb.AppendLine($"{name}[{start + i}] = {values[i]}  (0x{values[i] & 0xFFFF:X4})");
        return sb.ToString();
    }

    private static string FormatBools(string name, int start, bool[] values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
            sb.AppendLine($"{name}[{start + i}] = {values[i]}");
        return sb.ToString();
    }
}
