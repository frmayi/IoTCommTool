using System.IO.Ports;
using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus RTU 从站（串口，内置服务器后台运行）</summary>
public sealed class ModbusRtuSlaveConnection : ModbusSlaveConnectionBase
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;

    private SerialPort? _serialPort;

    public ModbusRtuSlaveConnection(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One, byte unitId = 1)
        : base(unitId)
    {
        _portName = portName;
        _baudRate = baudRate;
        _dataBits = dataBits;
        _parity = parity;
        _stopBits = stopBits;
        DisplayName = $"Modbus RTU 从站 {portName} 站号{unitId}";
    }

    public override ProtocolType Protocol => ProtocolType.ModbusRtuSlave;

    public string PortName => _portName;

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        SetState(ConnectionState.Connecting);
        var serial = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        var server = new ModbusRtuServer(UnitId, isAsynchronous: true);

        try
        {
            HookServerEvents(server);
            await Task.Run(() => server.Start(new ModbusRtuSerialPort(serial)), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { server.Stop(); } catch { }
            try { serial.Dispose(); } catch { }
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"启动 Modbus RTU 从站失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _serialPort = serial;
        Server = server;
        SetState(ConnectionState.Connected);
        RaiseLog($"Modbus RTU 从站已启动，串口 {_portName}（{_baudRate},{_dataBits},{_parity},{_stopBits}），站号 {UnitId}");
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

        if (_serialPort is not null)
        {
            try { if (_serialPort.IsOpen) _serialPort.Close(); } catch { }
            try { _serialPort.Dispose(); } catch { }
            _serialPort = null;
        }

        if (State != ConnectionState.Disconnected)
        {
            SetState(ConnectionState.Disconnected, "已关闭");
            RaiseLog("从站已停止");
        }

        return Task.CompletedTask;
    }
}
