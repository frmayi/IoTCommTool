using System.Net;
using System.Net.Sockets;
using IoTCommTool.Core.Abstractions;
using ProtocolType = IoTCommTool.Core.Abstractions.ProtocolType;

namespace IoTCommTool.Core.Udp;

/// <summary>UDP 连接：绑定本地端口接收，向配置的远端发送</summary>
public sealed class UdpConnection : ConnectionBase
{
    private readonly int _localPort;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private string? _remoteHost;
    private int? _remotePort;
    private IPEndPoint? _remoteEndpoint;

    private UdpClient? _client;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public UdpConnection(int localPort, string? remoteHost = null, int? remotePort = null)
    {
        _localPort = localPort;
        _remoteHost = remoteHost;
        _remotePort = remotePort;
        DisplayName = remoteHost is null || remotePort is null
            ? $"UDP :{localPort}"
            : $"UDP :{localPort} -> {remoteHost}:{remotePort}";
    }

    public override ProtocolType Protocol => ProtocolType.Udp;

    public int LocalPort => _localPort;

    /// <summary>修改默认发送目标，会清空已缓存的解析结果</summary>
    public void UpdateRemote(string host, int port)
    {
        _remoteHost = host;
        _remotePort = port;
        _remoteEndpoint = null;
    }

    public override Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return Task.CompletedTask;

        SetState(ConnectionState.Connecting);
        UdpClient client;
        try
        {
            client = new UdpClient(_localPort);
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"绑定本地端口 {_localPort} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _client = client;
        _receiveCts = new CancellationTokenSource();
        SetState(ConnectionState.Connected);
        RaiseLog($"已绑定本地端口 {_localPort}");
        _receiveTask = Task.Run(() => ReceiveLoopAsync(client, _receiveCts.Token));
        return Task.CompletedTask;
    }

    public override async Task CloseAsync()
    {
        if (State == ConnectionState.Disconnected)
            return;

        try { _receiveCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _client?.Close(); } catch { }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); } catch { }
            _receiveTask = null;
        }

        _client = null;
        SetState(ConnectionState.Disconnected, "已关闭");
        RaiseLog("连接已关闭");
    }

    /// <summary>发往配置的远端 endpoint（需先配置远端地址）</summary>
    public override async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var client = _client ?? throw new InvalidOperationException("未打开");
        var remote = await ResolveRemoteAsync(ct).ConfigureAwait(false);

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await client.SendAsync(data, remote, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>发往指定目标（不影响默认远端配置）</summary>
    public async Task SendToAsync(string host, int port, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var client = _client ?? throw new InvalidOperationException("未打开");
        var remote = await ResolveAsync(host, port, ct).ConfigureAwait(false);
        await client.SendAsync(data, remote, ct).ConfigureAwait(false);
    }

    private async Task<IPEndPoint> ResolveRemoteAsync(CancellationToken ct)
    {
        if (_remoteEndpoint is not null)
            return _remoteEndpoint;

        if (_remoteHost is null || _remotePort is null)
            throw new InvalidOperationException("未配置远端地址，请先调用 UpdateRemote");

        _remoteEndpoint = await ResolveAsync(_remoteHost, _remotePort.Value, ct).ConfigureAwait(false);
        return _remoteEndpoint;
    }

    private static async Task<IPEndPoint> ResolveAsync(string host, int port, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var ip))
            return new IPEndPoint(ip, port);

        var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"无法解析主机 {host}");

        return new IPEndPoint(addresses[0], port);
    }

    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken ct)
    {
        Exception? error = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(ct).ConfigureAwait(false);
                RaiseDataReceived(result.Buffer, result.RemoteEndPoint.ToString());
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ct.IsCancellationRequested || ex is ObjectDisposedException or SocketException)
        {
            if (!ct.IsCancellationRequested)
            {
                error = ex;
                RaiseLog($"接收异常: {ex.Message}", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            error = ex;
            RaiseLog($"接收异常: {ex.Message}", LogLevel.Error);
        }

        if (State == ConnectionState.Connected && error is not null)
            SetState(ConnectionState.Faulted, error.Message);
    }
}
