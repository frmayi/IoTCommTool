using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public abstract partial class ModbusSlaveViewModelBase : ConnectionViewModelBase
{
    protected ModbusSlaveViewModelBase(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
    }

    public IReadOnlyList<string> Areas { get; } = new[] { "保持寄存器", "输入寄存器", "线圈", "离散输入" };

    [ObservableProperty]
    public partial int AreaIndex { get; set; }

    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0";

    [ObservableProperty]
    public partial string Count { get; set; } = "10";

    public ObservableCollection<RegisterRow> Rows { get; } = new();

    protected ModbusSlaveConnectionBase? Slave => Connection as ModbusSlaveConnectionBase;

    protected override string OpenVerb => "启动";

    protected override string CloseVerb => "停止";

    protected override string ConnectedStateText => "运行中";

    [RelayCommand]
    private void RefreshRegisters()
    {
        ModbusSlaveConnectionBase? slave = Slave;
        if (slave is null)
        {
            AppendLog("从站未启动，无法读取寄存器", LogLevel.Warning);
            return;
        }

        try
        {
            int start = InputParser.ParseInt(StartAddress, 0, 65535, "起始地址");
            int count = InputParser.ParseInt(Count, 1, 100, "数量");
            Rows.Clear();
            switch (AreaIndex)
            {
                case 0:
                    foreach (short v in slave.GetHoldingRegisters(start, count))
                        Rows.Add(new RegisterRow(start + Rows.Count, v.ToString()));
                    break;
                case 1:
                    foreach (short v in slave.GetInputRegisters(start, count))
                        Rows.Add(new RegisterRow(start + Rows.Count, v.ToString()));
                    break;
                case 2:
                    foreach (bool v in slave.GetCoils(start, count))
                        Rows.Add(new RegisterRow(start + Rows.Count, v ? "True" : "False"));
                    break;
                case 3:
                    foreach (bool v in slave.GetDiscreteInputs(start, count))
                        Rows.Add(new RegisterRow(start + Rows.Count, v ? "True" : "False"));
                    break;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"读取失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private void WriteRegisters()
    {
        ModbusSlaveConnectionBase? slave = Slave;
        if (slave is null)
        {
            AppendLog("从站未启动，无法写入寄存器", LogLevel.Warning);
            return;
        }

        if (Rows.Count == 0)
        {
            AppendLog("请先点击“刷新”读取要修改的寄存器", LogLevel.Warning);
            return;
        }

        try
        {
            int start = Rows[0].Address;
            switch (AreaIndex)
            {
                case 0:
                    slave.SetHoldingRegisters(start, Rows.Select(r => InputParser.ParseShort(r.Value, $"寄存器[{r.Address}]")).ToArray());
                    break;
                case 1:
                    slave.SetInputRegisters(start, Rows.Select(r => InputParser.ParseShort(r.Value, $"寄存器[{r.Address}]")).ToArray());
                    break;
                case 2:
                    slave.SetCoils(start, Rows.Select(r => InputParser.ParseBool(r.Value, $"线圈[{r.Address}]")).ToArray());
                    break;
                case 3:
                    slave.SetDiscreteInputs(start, Rows.Select(r => InputParser.ParseBool(r.Value, $"离散输入[{r.Address}]")).ToArray());
                    break;
            }

            AppendLog($"已写入 {Rows.Count} 个{Areas[AreaIndex]}（起始地址 {start}）");
        }
        catch (Exception ex)
        {
            AppendLog($"写入失败：{ex.Message}", LogLevel.Error);
        }
    }
}
