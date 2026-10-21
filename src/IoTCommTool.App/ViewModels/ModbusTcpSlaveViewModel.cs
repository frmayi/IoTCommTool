using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class ModbusTcpSlaveViewModel : ModbusSlaveViewModelBase
{
    public ModbusTcpSlaveViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.ModbusTcpSlave;

    [ObservableProperty]
    public partial string Port { get; set; } = "502";

    [ObservableProperty]
    public partial string UnitId { get; set; } = "1";

    partial void OnPortChanged(string value) => RefreshAutoTitle();

    partial void OnUnitIdChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
        => new ModbusTcpSlaveConnection(InputParser.ParsePort(Port), InputParser.ParseByte(UnitId, "站号"));

    protected override string SerializeParams()
        => JsonSerializer.Serialize(new ModbusTcpSlaveParams
        {
            Port = InputParser.ParsePort(Port),
            UnitId = InputParser.ParseByte(UnitId, "站号"),
        });

    public override void LoadParamsJson(string json)
    {
        try
        {
            ModbusTcpSlaveParams? p = JsonSerializer.Deserialize<ModbusTcpSlaveParams>(json);
            if (p is null)
                return;
            Port = p.Port.ToString();
            UnitId = p.UnitId.ToString();
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private void RefreshAutoTitle() => UpdateAutoTitle($"Modbus TCP从站 :{Port} 站号{UnitId}");
}
