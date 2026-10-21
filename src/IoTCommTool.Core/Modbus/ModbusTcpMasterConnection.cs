using System.Net;
using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus TCP 主站</summary>
public sealed class ModbusTcpMasterConnection : ModbusMasterConnectionBase
{
    private readonly string _host;
    private readonly int _port;

    public ModbusTcpMasterConnection(string host, int port = 502)
    {
        _host = host;
        _port = port;
        DisplayName = $"Modbus TCP 主站 {host}:{port}";
    }

    public override ProtocolType Protocol => ProtocolType.ModbusTcpMaster;

    public string Host => _host;

    public int Port => _port;

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        SetState(ConnectionState.Connecting);
        var client = new ModbusTcpClient();
        try
        {
            var endpoint = await ResolveEndpointAsync(ct).ConfigureAwait(false);
            await Task.Run(() => client.Connect(endpoint), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            client.Dispose();
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"连接 {_host}:{_port} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        Client = client;
        SetState(ConnectionState.Connected);
        RaiseLog($"已连接 {_host}:{_port}");
    }

    public override Task CloseAsync()
    {
        var client = Client;
        Client = null;

        if (client is not null)
        {
            try { ((ModbusTcpClient)client).Disconnect(); } catch { }
            try { ((ModbusTcpClient)client).Dispose(); } catch { }
        }

        if (State != ConnectionState.Disconnected)
        {
            SetState(ConnectionState.Disconnected, "已关闭");
            RaiseLog("连接已关闭");
        }

        return Task.CompletedTask;
    }

    private async Task<IPEndPoint> ResolveEndpointAsync(CancellationToken ct)
    {
        if (IPAddress.TryParse(_host, out var ip))
            return new IPEndPoint(ip, _port);

        var addresses = await Dns.GetHostAddressesAsync(_host, ct).ConfigureAwait(false);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"无法解析主机 {_host}");

        return new IPEndPoint(addresses[0], _port);
    }
}
