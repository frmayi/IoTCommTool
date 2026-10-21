using System.Net.Sockets;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Tcp;

/// <summary>TCP 服务器侧的一个客户端会话</summary>
public sealed class ClientSession : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    internal ClientSession(TcpClient client)
    {
        _client = client;
        RemoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? string.Empty;
    }

    /// <summary>会话唯一标识</summary>
    public Guid SessionId { get; } = Guid.NewGuid();

    /// <summary>远端地址，如 192.168.1.10:5000</summary>
    public string RemoteEndpoint { get; }

    /// <summary>该会话收到数据时触发</summary>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>该会话结束时触发（对端断开或被踢出）</summary>
    public event EventHandler<ClientSession>? Closed;

    internal void Start()
    {
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    /// <summary>向该会话发送数据</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var stream = _client.GetStream();

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

    /// <summary>关闭该会话</summary>
    public async Task CloseAsync()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _client.Close(); } catch { }

        if (_loopTask is not null)
        {
            try { await _loopTask.ConfigureAwait(false); } catch { }
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];

        try
        {
            var stream = _client.GetStream();
            while (!ct.IsCancellationRequested)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (n == 0)
                    break;

                DataReceived?.Invoke(this, new DataReceivedEventArgs(buffer.AsSpan(0, n).ToArray(), RemoteEndpoint));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
            // 连接异常视为会话结束
        }

        try { _client.Close(); } catch { }
        Closed?.Invoke(this, this);
    }
}
