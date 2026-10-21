using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Tcp;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class TcpClientViewModel : RawConnectionViewModelBase
{
    private static readonly LocalAddressOption AutoLocalBind = new(string.Empty, "自动（系统路由）");
    private bool _syncingLocalBind;

    public TcpClientViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        LocalNetworkAddressService.Instance.Changed += OnLocalAddressesChanged;
        ReloadLocalBindChoices();
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.TcpClient;

    public override bool SupportsRegisterPacket => true;

    public ObservableCollection<LocalAddressOption> LocalBindChoices { get; } = new();

    [ObservableProperty]
    public partial LocalAddressOption? SelectedLocalBind { get; set; }

    [ObservableProperty]
    public partial string LocalBindHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Host { get; set; } = "127.0.0.1";

    [ObservableProperty]
    public partial string Port { get; set; } = "502";

    partial void OnSelectedLocalBindChanged(LocalAddressOption? value)
    {
        if (_syncingLocalBind || value is null)
            return;

        if (!string.Equals(LocalBindHost, value.Address, StringComparison.Ordinal))
            LocalBindHost = value.Address;
    }

    partial void OnHostChanged(string value) => RefreshAutoTitle();

    partial void OnPortChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
    {
        IPAddress? localAddress = null;
        if (!string.IsNullOrWhiteSpace(LocalBindHost))
        {
            if (!IPAddress.TryParse(LocalBindHost.Trim(), out localAddress))
                throw new FormatException($"本地出口 IP 无效：{LocalBindHost}");
        }

        return new TcpClientConnection(
            InputParser.RequireHost(Host),
            InputParser.ParsePort(Port),
            localAddress);
    }

    protected override string SerializeParams()
        => JsonSerializer.Serialize(new TcpClientParams
        {
            Host = Host.Trim(),
            Port = InputParser.ParsePort(Port),
            LocalBindHost = string.IsNullOrWhiteSpace(LocalBindHost) ? null : LocalBindHost.Trim(),
            RegisterEnabled = RegisterEnabled,
            RegisterText = string.IsNullOrWhiteSpace(RegisterText) ? null : RegisterText,
            RegisterHex = RegisterHex,
        });

    public override void LoadParamsJson(string json)
    {
        try
        {
            TcpClientParams? p = JsonSerializer.Deserialize<TcpClientParams>(json);
            if (p is null)
                return;
            Host = p.Host;
            Port = p.Port.ToString();
            LocalBindHost = p.LocalBindHost ?? string.Empty;
            SelectLocalBind(LocalBindHost);
            RegisterHex = p.RegisterHex;
            RegisterText = p.RegisterText ?? string.Empty;
            RegisterEnabled = p.RegisterEnabled;
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        LocalNetworkAddressService.Instance.Changed -= OnLocalAddressesChanged;
        await base.DisposeAsync();
    }

    private void ReloadLocalBindChoices()
    {
        string current = SelectedLocalBind?.Address ?? LocalBindHost;
        IEnumerable<LocalAddressOption> addresses = LocalNetworkAddressService.Instance.GetSnapshot()
            .Where(option => option.Address.Length > 0 && option.Address != "0.0.0.0");

        _syncingLocalBind = true;
        try
        {
            LocalBindChoices.Clear();
            LocalBindChoices.Add(AutoLocalBind);
            foreach (LocalAddressOption option in addresses)
                LocalBindChoices.Add(option);
        }
        finally
        {
            _syncingLocalBind = false;
        }

        SelectLocalBind(current);
    }

    private void SelectLocalBind(string? address)
    {
        string target = address?.Trim() ?? string.Empty;
        if (target.Length == 0)
        {
            ApplySelectedLocalBind(AutoLocalBind);
            return;
        }

        LocalAddressOption? option = LocalBindChoices.FirstOrDefault(
            item => string.Equals(item.Address, target, StringComparison.Ordinal));
        if (option is null)
        {
            option = new LocalAddressOption(target, $"已失效 · {target}");
            _syncingLocalBind = true;
            try
            {
                LocalBindChoices.Add(option);
            }
            finally
            {
                _syncingLocalBind = false;
            }

            AppendLog($"本地出口 {target} 当前不可用，已保留配置，请重新选择。", LogLevel.Warning);
        }

        ApplySelectedLocalBind(option);
    }

    private void ApplySelectedLocalBind(LocalAddressOption option)
    {
        if (ReferenceEquals(SelectedLocalBind, option))
            return;

        _syncingLocalBind = true;
        try
        {
            SelectedLocalBind = option;
        }
        finally
        {
            _syncingLocalBind = false;
        }
    }

    private void OnLocalAddressesChanged(object? sender, EventArgs e)
    {
        Post(() =>
        {
            if (State is ConnectionState.Connected or ConnectionState.Connecting)
                AppendLog("检测到网络地址变化；已有 TCP 连接不会自动迁移，必要时请断开重连。", LogLevel.Warning);

            ReloadLocalBindChoices();
        });
    }

    private void RefreshAutoTitle() => UpdateAutoTitle($"TCP客户端 {Host}:{Port}");
}
