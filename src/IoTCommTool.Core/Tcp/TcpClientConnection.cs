using System.Linq;
using System.Net;
using System.Net.Sockets;
using IoTCommTool.Core.Abstractions;
using ProtocolType = IoTCommTool.Core.Abstractions.ProtocolType;

namespace IoTCommTool.Core.Tcp;

/// <summary>TCP 客户端连接，后台循环接收</summary>
public sealed class TcpClientConnection : ConnectionBase
{
    private readonly string _host;
    private readonly int _port;
    private readonly IPAddress? _localAddress;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private TcpClient? _client;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public TcpClientConnection(string host, int port)
        : this(host, port, null)
    {
    }

    public TcpClientConnection(string host, int port, IPAddress? localAddress)
    {
        _host = host;
        _port = port;
        _localAddress = localAddress;
        DisplayName = localAddress is null
            ? $"TCP Client {host}:{port}"
            : $"TCP Client {host}:{port} @ {localAddress}";
    }

    public override ProtocolType Protocol => ProtocolType.TcpClient;

    public string Host => _host;

    public int Port => _port;

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        SetState(ConnectionState.Connecting);
        var client = _localAddress is null
            ? new TcpClient()
            : new TcpClient(_localAddress.AddressFamily);
        try
        {
            if (_localAddress is not null)
            {
                client.Client.Bind(new IPEndPoint(_localAddress, 0));
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(_host, ct).ConfigureAwait(false);
                IPAddress? remote = addresses.FirstOrDefault(
                    address => address.AddressFamily == _localAddress.AddressFamily);
                if (remote is null)
                    throw new InvalidOperationException($"无法将 {_host} 解析为 {(_localAddress.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")} 地址");

                await client.ConnectAsync(remote, _port, ct).ConfigureAwait(false);
            }
            else
            {
                await client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            client.Dispose();
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"连接 {_host}:{_port} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _client = client;
        _receiveCts = new CancellationTokenSource();
        SetState(ConnectionState.Connected);
        string localEndpoint = client.Client.LocalEndPoint?.ToString() ?? "自动";
        RaiseLog($"已连接 {_host}:{_port}（本机 {localEndpoint}）");
        _receiveTask = Task.Run(() => ReceiveLoopAsync(client, _receiveCts.Token));
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

    public override async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var client = _client ?? throw new InvalidOperationException("未连接");
        var stream = client.GetStream();

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(TcpClient client, CancellationToken ct)
    {
        var buffer = new byte[65536];
        string? remote = client.Client.RemoteEndPoint?.ToString();
        Exception? error = null;

        try
        {
            var stream = client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (n == 0)
                    break;

                RaiseDataReceived(buffer.AsSpan(0, n).ToArray(), remote);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            error = ex;
            RaiseLog($"接收异常: {ex.Message}", LogLevel.Error);
        }

        if (State == ConnectionState.Connected)
        {
            if (error is null)
            {
                SetState(ConnectionState.Disconnected, "对端断开");
                RaiseLog("对端已断开连接", LogLevel.Warning);
            }
            else
            {
                SetState(ConnectionState.Faulted, error.Message);
            }
        }
    }
}
