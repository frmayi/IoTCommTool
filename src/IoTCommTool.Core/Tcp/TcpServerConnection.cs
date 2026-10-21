using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using IoTCommTool.Core.Abstractions;
using ProtocolType = IoTCommTool.Core.Abstractions.ProtocolType;

namespace IoTCommTool.Core.Tcp;

/// <summary>TCP 服务器连接，支持多客户端会话、广播与定向发送</summary>
public sealed class TcpServerConnection : ConnectionBase
{
    private readonly IPAddress _listenAddress;
    private readonly int _port;
    private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptTask;

    public TcpServerConnection(int port)
        : this(null, port)
    {
    }

    public TcpServerConnection(string? listenHost, int port)
    {
        _listenAddress = ParseListenAddress(listenHost);
        _port = port;
        DisplayName = $"TCP Server {_listenAddress}:{port}";
    }

    public override ProtocolType Protocol => ProtocolType.TcpServer;

    public int Port => _port;

    /// <summary>当前会话快照</summary>
    public IReadOnlyCollection<ClientSession> Sessions => _sessions.Values.ToArray();

    /// <summary>新客户端接入时触发</summary>
    public event EventHandler<ClientSession>? SessionAdded;

    /// <summary>客户端会话结束时触发</summary>
    public event EventHandler<ClientSession>? SessionRemoved;

    public override Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return Task.CompletedTask;

        SetState(ConnectionState.Connecting);
        try
        {
            _listener = new TcpListener(_listenAddress, _port);
            _listener.Start();
        }
        catch (Exception ex)
        {
            _listener = null;
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"监听端口 {_port} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _acceptCts = new CancellationTokenSource();
        SetState(ConnectionState.Connected);
        RaiseLog($"已监听 {_listenAddress}:{_port}");
        _acceptTask = Task.Run(() => AcceptLoopAsync(_listener, _acceptCts.Token));
        return Task.CompletedTask;
    }

    public override async Task CloseAsync()
    {
        if (State == ConnectionState.Disconnected)
            return;

        try { _acceptCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _listener?.Stop(); } catch { }

        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch { }
            _acceptTask = null;
        }

        foreach (var session in _sessions.Values.ToArray())
        {
            try { await session.CloseAsync().ConfigureAwait(false); } catch { }
        }

        _listener = null;
        SetState(ConnectionState.Disconnected, "已关闭");
        RaiseLog("服务器已关闭");
    }

    /// <summary>广播：发送给所有会话</summary>
    public override async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        foreach (var session in _sessions.Values.ToArray())
        {
            try
            {
                await session.SendAsync(data, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RaiseLog($"发送到 {session.RemoteEndpoint} 失败: {ex.Message}", LogLevel.Warning);
            }
        }
    }

    /// <summary>发送给指定会话</summary>
    public Task SendToAsync(Guid sessionId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _sessions.TryGetValue(sessionId, out var session)
            ? session.SendAsync(data, ct)
            : throw new KeyNotFoundException($"会话 {sessionId} 不存在");

    /// <summary>踢掉指定会话</summary>
    public async Task KickAsync(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            RaiseLog($"踢出客户端 {session.RemoteEndpoint}", LogLevel.Warning);
            await session.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                var session = new ClientSession(client);
                session.DataReceived += (_, e) => RaiseDataReceived(e.Data, e.RemoteEndpoint);
                session.Closed += (_, _) => RemoveSession(session);
                _sessions[session.SessionId] = session;
                session.Start();

                RaiseLog($"客户端接入: {session.RemoteEndpoint}");
                SessionAdded?.Invoke(this, session);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ct.IsCancellationRequested || ex is ObjectDisposedException or SocketException)
        {
            if (State == ConnectionState.Connected && !ct.IsCancellationRequested)
            {
                SetState(ConnectionState.Faulted, ex.Message);
                RaiseLog($"监听异常: {ex.Message}", LogLevel.Error);
            }
        }
        catch (Exception ex)
        {
            if (State == ConnectionState.Connected)
            {
                SetState(ConnectionState.Faulted, ex.Message);
                RaiseLog($"监听异常: {ex.Message}", LogLevel.Error);
            }
        }
    }

    private void RemoveSession(ClientSession session)
    {
        if (_sessions.TryRemove(session.SessionId, out _))
        {
            RaiseLog($"客户端断开: {session.RemoteEndpoint}");
            SessionRemoved?.Invoke(this, session);
        }
    }

    private static IPAddress ParseListenAddress(string? listenHost)
    {
        string? host = listenHost?.Trim();
        if (string.IsNullOrEmpty(host) || host == "0.0.0.0")
            return IPAddress.Any;
        try
        {
            return IPAddress.Parse(host);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"监听 IP 地址无效：{listenHost}", nameof(listenHost), ex);
        }
    }
}
