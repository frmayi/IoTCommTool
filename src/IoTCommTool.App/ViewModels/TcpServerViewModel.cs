using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Models;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Core.Tcp;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class TcpServerViewModel : RawConnectionViewModelBase
{
    private TcpServerConnection? _server;
    private int _sessionCounter;
    private bool _syncingListenAddress;

    public TcpServerViewModel(ConnectionConfigRepository repository, Action savedConfigsChanged)
        : base(repository, savedConfigsChanged)
    {
        LocalNetworkAddressService.Instance.Changed += OnLocalAddressesChanged;
        ReloadLocalAddresses();
        RefreshAutoTitle();
    }

    public override ProtocolType Protocol => ProtocolType.TcpServer;

    public ObservableCollection<LocalAddressOption> LocalIpChoices { get; } = new();

    [ObservableProperty]
    public partial LocalAddressOption? SelectedListenAddress { get; set; }

    public ObservableCollection<SessionItem> Sessions { get; } = new();

    [ObservableProperty]
    public partial SessionItem? SelectedSession { get; set; }

    public bool HasSelectedSession => SelectedSession is not null;

    partial void OnSelectedSessionChanged(SessionItem? value) => OnPropertyChanged(nameof(HasSelectedSession));

    [ObservableProperty]
    public partial string Host { get; set; } = "0.0.0.0";

    partial void OnHostChanged(string value)
    {
        RefreshAutoTitle();
        if (!_syncingListenAddress)
            SelectListenAddress(value);
    }

    partial void OnSelectedListenAddressChanged(LocalAddressOption? value)
    {
        if (_syncingListenAddress || value is null)
            return;

        if (!string.Equals(Host, value.Address, StringComparison.Ordinal))
            Host = value.Address;
    }

    [ObservableProperty]
    public partial string Port { get; set; } = "5000";

    partial void OnPortChanged(string value) => RefreshAutoTitle();

    protected override IConnection CreateConnection()
    {
        _server = new TcpServerConnection(Host, InputParser.ParsePort(Port));
        return _server;
    }

    protected override void OnOpened()
    {
        TcpServerConnection? server = _server;
        if (server is null)
            return;

        server.SessionAdded += OnSessionAdded;
        server.SessionRemoved += OnSessionRemoved;
        foreach (ClientSession session in server.Sessions)
            TrackSession(session);
    }

    protected override void OnClosed() => UnhookServer();

    protected override Task SendPayloadAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        TcpServerConnection server = _server ?? throw new InvalidOperationException("未连接");
        SessionItem? target = SelectedSession;
        return target is null
            ? server.SendAsync(data, ct)
            : server.SendToAsync(target.Session.SessionId, data, ct);
    }

    [RelayCommand]
    private async Task KickSessionAsync()
    {
        TcpServerConnection? server = _server;
        SessionItem? target = SelectedSession;
        if (server is null || target is null)
        {
            AppendLog("请先在会话列表中选择要踢除的客户端", LogLevel.Warning);
            return;
        }

        try
        {
            await server.KickAsync(target.Session.SessionId);
        }
        catch (Exception ex)
        {
            AppendLog($"踢除失败：{ex.Message}", LogLevel.Error);
        }
    }

    protected override string SerializeParams()
        => JsonSerializer.Serialize(new TcpServerParams { Host = Host, Port = InputParser.ParsePort(Port) });

    public override void LoadParamsJson(string json)
    {
        try
        {
            TcpServerParams? p = JsonSerializer.Deserialize<TcpServerParams>(json);
            if (p is null)
                return;
            Host = string.IsNullOrWhiteSpace(p.Host) ? "0.0.0.0" : p.Host;
            SelectListenAddress(Host);
            Port = p.Port.ToString();
        }
        catch (Exception ex)
        {
            AppendLog($"配置解析失败：{ex.Message}", LogLevel.Warning);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        LocalNetworkAddressService.Instance.Changed -= OnLocalAddressesChanged;
        UnhookServer();
        await base.DisposeAsync();
    }

    private void TrackSession(ClientSession session)
    {
        session.DataReceived += OnSessionDataReceived;
        Post(() =>
        {
            if (FindItem(session.SessionId) is null)
                Sessions.Add(new SessionItem(session, $"会话 {++_sessionCounter}"));
        });
    }

    private void OnSessionAdded(object? sender, ClientSession session) => TrackSession(session);

    private void OnSessionRemoved(object? sender, ClientSession session)
    {
        session.DataReceived -= OnSessionDataReceived;
        Post(() =>
        {
            SessionItem? item = FindItem(session.SessionId);
            if (item is null)
                return;
            if (ReferenceEquals(SelectedSession, item))
                SelectedSession = null;
            Sessions.Remove(item);
        });
    }

    private void OnSessionDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (sender is ClientSession session)
            Post(() => FindItem(session.SessionId)?.IncrementReceived());
    }

    private SessionItem? FindItem(Guid sessionId)
    {
        foreach (SessionItem item in Sessions)
        {
            if (item.Session.SessionId == sessionId)
                return item;
        }

        return null;
    }

    private void UnhookServer()
    {
        if (_server is not null)
        {
            _server.SessionAdded -= OnSessionAdded;
            _server.SessionRemoved -= OnSessionRemoved;
        }

        Post(() =>
        {
            Sessions.Clear();
            SelectedSession = null;
        });
    }

    private void RefreshAutoTitle() => UpdateAutoTitle($"TCP服务端 {Host}:{Port}");

    private void ReloadLocalAddresses()
    {
        string current = SelectedListenAddress?.Address ?? Host;
        IReadOnlyList<LocalAddressOption> addresses = LocalNetworkAddressService.Instance.GetSnapshot();

        _syncingListenAddress = true;
        try
        {
            LocalIpChoices.Clear();
            foreach (LocalAddressOption option in addresses)
                LocalIpChoices.Add(option);
        }
        finally
        {
            _syncingListenAddress = false;
        }

        SelectListenAddress(string.IsNullOrWhiteSpace(current) ? "0.0.0.0" : current);
    }

    private void SelectListenAddress(string? address)
    {
        string target = string.IsNullOrWhiteSpace(address) ? "0.0.0.0" : address.Trim();
        LocalAddressOption? option = LocalIpChoices.FirstOrDefault(
            item => string.Equals(item.Address, target, StringComparison.Ordinal));

        if (option is null)
        {
            option = new LocalAddressOption(target, $"已失效 · {target}");
            _syncingListenAddress = true;
            try
            {
                LocalIpChoices.Add(option);
            }
            finally
            {
                _syncingListenAddress = false;
            }

            AppendLog($"监听地址 {target} 当前不可用，已保留配置，请重新选择。", LogLevel.Warning);
        }

        if (ReferenceEquals(SelectedListenAddress, option))
            return;

        _syncingListenAddress = true;
        try
        {
            SelectedListenAddress = option;
        }
        finally
        {
            _syncingListenAddress = false;
        }
    }

    private void OnLocalAddressesChanged(object? sender, EventArgs e)
    {
        Post(() =>
        {
            if (State is ConnectionState.Connected or ConnectionState.Connecting)
                AppendLog("检测到网络地址变化，当前监听器不会自动重绑。", LogLevel.Warning);

            ReloadLocalAddresses();
        });
    }
}
