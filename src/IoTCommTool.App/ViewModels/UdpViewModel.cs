using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Udp;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class UdpViewModel : RawConnectionViewModelBase
{
    public UdpViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.Udp;

    public override bool SupportsRegisterPacket => true;

    [ObservableProperty]
    public partial string LocalPort { get; set; } = "5001";

    [ObservableProperty]
    public partial string RemoteHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemotePort { get; set; } = string.Empty;

    partial void OnLocalPortChanged(string value) => RefreshAutoTitle();

    partial void OnRemoteHostChanged(string value) => RefreshAutoTitle();

    partial void OnRemotePortChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
    {
        int localPort = InputParser.ParsePort(LocalPort, "本地端口");
        string? host = string.IsNullOrWhiteSpace(RemoteHost) ? null : RemoteHost.Trim();
        int? remotePort = host is null ? null : InputParser.ParsePort(RemotePort, "远端端口");
        return new UdpConnection(localPort, host, remotePort);
    }

    protected override string SerializeParams()
    {
        string? host = string.IsNullOrWhiteSpace(RemoteHost) ? null : RemoteHost.Trim();
        return JsonSerializer.Serialize(new UdpParams
        {
            LocalPort = InputParser.ParsePort(LocalPort, "本地端口"),
            RemoteHost = host,
            RemotePort = host is null ? null : InputParser.ParsePort(RemotePort, "远端端口"),
            RegisterEnabled = RegisterEnabled,
            RegisterText = string.IsNullOrWhiteSpace(RegisterText) ? null : RegisterText,
            RegisterHex = RegisterHex,
        });
    }

    public override void LoadParamsJson(string json)
    {
        try
        {
            UdpParams? p = JsonSerializer.Deserialize<UdpParams>(json);
            if (p is null)
                return;
            LocalPort = p.LocalPort.ToString();
            RemoteHost = p.RemoteHost ?? string.Empty;
            RemotePort = p.RemotePort?.ToString() ?? string.Empty;
            RegisterHex = p.RegisterHex;
            RegisterText = p.RegisterText ?? string.Empty;
            RegisterEnabled = p.RegisterEnabled;
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private void RefreshAutoTitle()
        => UpdateAutoTitle(string.IsNullOrWhiteSpace(RemoteHost)
            ? $"UDP :{LocalPort}"
            : $"UDP :{LocalPort} -> {RemoteHost}:{RemotePort}");
}
