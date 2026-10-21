using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Modbus;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class ModbusTcpMasterViewModel : ModbusMasterViewModelBase
{
    public ModbusTcpMasterViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.ModbusTcpMaster;

    [ObservableProperty]
    public partial string Host { get; set; } = "127.0.0.1";

    [ObservableProperty]
    public partial string Port { get; set; } = "502";

    partial void OnHostChanged(string value) => RefreshAutoTitle();

    partial void OnPortChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
        => new ModbusTcpMasterConnection(InputParser.RequireHost(Host), InputParser.ParsePort(Port));

    protected override string SerializeParams()
        => JsonSerializer.Serialize(new ModbusTcpMasterParams { Host = Host.Trim(), Port = InputParser.ParsePort(Port) });

    public override void LoadParamsJson(string json)
    {
        try
        {
            ModbusTcpMasterParams? p = JsonSerializer.Deserialize<ModbusTcpMasterParams>(json);
            if (p is null)
                return;
            Host = p.Host;
            Port = p.Port.ToString();
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private void RefreshAutoTitle() => UpdateAutoTitle($"Modbus TCP主站 {Host}:{Port}");
}
