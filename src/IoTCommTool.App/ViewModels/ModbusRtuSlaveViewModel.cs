using System;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class ModbusRtuSlaveViewModel : ModbusSlaveViewModelBase
{
    public ModbusRtuSlaveViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        Serial = new SerialPortSettingsViewModel();
        Serial.PropertyChanged += OnSerialPropertyChanged;
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.ModbusRtuSlave;

    public SerialPortSettingsViewModel Serial { get; }

    [ObservableProperty]
    public partial string UnitId { get; set; } = "1";

    partial void OnUnitIdChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
    {
        SerialSettingsSnapshot s = Serial.Snapshot();
        return new ModbusRtuSlaveConnection(s.PortName, s.BaudRate, s.DataBits, s.Parity, s.StopBits, InputParser.ParseByte(UnitId, "站号"));
    }

    protected override string SerializeParams()
    {
        SerialSettingsSnapshot s = Serial.Snapshot();
        return JsonSerializer.Serialize(new ModbusRtuSlaveParams
        {
            PortName = s.PortName,
            BaudRate = s.BaudRate,
            DataBits = s.DataBits,
            Parity = s.Parity.ToString(),
            StopBits = s.StopBits.ToString(),
            UnitId = InputParser.ParseByte(UnitId, "站号"),
        });
    }

    public override void LoadParamsJson(string json)
    {
        try
        {
            ModbusRtuSlaveParams? p = JsonSerializer.Deserialize<ModbusRtuSlaveParams>(json);
            if (p is null)
                return;
            Serial.Load(p.PortName, p.BaudRate, p.DataBits, p.Parity, p.StopBits);
            UnitId = p.UnitId.ToString();
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private void OnSerialPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SerialPortSettingsViewModel.SelectedPortName))
            RefreshAutoTitle();
    }

    private void RefreshAutoTitle() => UpdateAutoTitle($"Modbus RTU从站 {Serial.SelectedPortName ?? "(未选串口)"} 站号{UnitId}");
}
