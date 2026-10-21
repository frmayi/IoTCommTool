using System.IO.Ports;
using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus RTU 主站（串口）</summary>
public sealed class ModbusRtuMasterConnection : ModbusMasterConnectionBase
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;

    private SerialPort? _serialPort;

    public ModbusRtuMasterConnection(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        _portName = portName;
        _baudRate = baudRate;
        _dataBits = dataBits;
        _parity = parity;
        _stopBits = stopBits;
        DisplayName = $"Modbus RTU 主站 {portName} {baudRate},{dataBits},{parity},{stopBits}";
    }

    public override ProtocolType Protocol => ProtocolType.ModbusRtuMaster;

    public string PortName => _portName;

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        SetState(ConnectionState.Connecting);
        var client = new ModbusRtuClient();
        var serial = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        try
        {
            await Task.Run(() => client.Initialize(new ModbusRtuSerialPort(serial), ModbusEndianness.LittleEndian), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { serial.Dispose(); } catch { }
            try { client.Dispose(); } catch { }
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"打开串口 {_portName} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _serialPort = serial;
        Client = client;
        SetState(ConnectionState.Connected);
        RaiseLog($"已打开串口 {_portName}（{_baudRate},{_dataBits},{_parity},{_stopBits}）");
    }

    public override Task CloseAsync()
    {
        var client = Client;
        Client = null;

        if (client is not null)
        {
            try { ((ModbusRtuClient)client).Close(); } catch { }
            try { ((ModbusRtuClient)client).Dispose(); } catch { }
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
            RaiseLog("串口已关闭");
        }

        return Task.CompletedTask;
    }
}
