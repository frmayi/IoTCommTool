using System.Net;
using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus TCP 从站（内置服务器后台运行）</summary>
public sealed class ModbusTcpSlaveConnection : ModbusSlaveConnectionBase
{
    private readonly int _port;

    public ModbusTcpSlaveConnection(int port = 502, byte unitId = 1)
        : base(unitId)
    {
        _port = port;
        DisplayName = $"Modbus TCP 从站 :{port} 站号{unitId}";
    }

    public override ProtocolType Protocol => ProtocolType.ModbusTcpSlave;

    public int Port => _port;

    public override Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return Task.CompletedTask;

        SetState(ConnectionState.Connecting);
        var server = new ModbusTcpServer(isAsynchronous: true);
        try
        {
            if (!server.UnitIdentifiers.Contains(UnitId))
                server.AddUnit(UnitId);

            HookServerEvents(server);
            server.Start(new IPEndPoint(IPAddress.Any, _port));
        }
        catch (Exception ex)
        {
            try { server.Stop(); } catch { }
            try { server.Dispose(); } catch { }
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"启动 Modbus TCP 从站失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        Server = server;
        SetState(ConnectionState.Connected);
        RaiseLog($"Modbus TCP 从站已启动，监听 0.0.0.0:{_port}，站号 {UnitId}");
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        var server = Server;
        Server = null;

        if (server is not null)
        {
            try { server.Stop(); } catch { }
            try { server.Dispose(); } catch { }
        }

        if (State != ConnectionState.Disconnected)
        {
            SetState(ConnectionState.Disconnected, "已关闭");
            RaiseLog("从站已停止");
        }

        return Task.CompletedTask;
    }
}
