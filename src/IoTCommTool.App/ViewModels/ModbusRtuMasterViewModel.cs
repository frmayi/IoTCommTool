using System;
using System.ComponentModel;
using System.Text.Json;
using IoTCommTool.App.Models;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class ModbusRtuMasterViewModel : ModbusMasterViewModelBase
{
    public ModbusRtuMasterViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        Serial = new SerialPortSettingsViewModel();
        Serial.PropertyChanged += OnSerialPropertyChanged;
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.ModbusRtuMaster;

    public SerialPortSettingsViewModel Serial { get; }

    protected override IConnection CreateConnection()
    {
        SerialSettingsSnapshot s = Serial.Snapshot();
        return new ModbusRtuMasterConnection(s.PortName, s.BaudRate, s.DataBits, s.Parity, s.StopBits);
    }

    protected override string SerializeParams()
    {
        SerialSettingsSnapshot s = Serial.Snapshot();
        return JsonSerializer.Serialize(new ModbusRtuMasterParams
        {
            PortName = s.PortName,
            BaudRate = s.BaudRate,
            DataBits = s.DataBits,
            Parity = s.Parity.ToString(),
            StopBits = s.StopBits.ToString(),
        });
    }

    public override void LoadParamsJson(string json)
    {
        try
        {
            ModbusRtuMasterParams? p = JsonSerializer.Deserialize<ModbusRtuMasterParams>(json);
            if (p is null)
                return;
            Serial.Load(p.PortName, p.BaudRate, p.DataBits, p.Parity, p.StopBits);
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

    private void RefreshAutoTitle() => UpdateAutoTitle($"Modbus RTU主站 {Serial.SelectedPortName ?? "(未选串口)"}");
}
