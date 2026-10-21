using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus 从站基类，提供内部寄存器/线圈区的读写访问</summary>
public abstract class ModbusSlaveConnectionBase : ConnectionBase
{
    protected ModbusSlaveConnectionBase(byte unitId)
    {
        UnitId = unitId;
    }

    protected ModbusServer? Server { get; set; }

    /// <summary>本站站号</summary>
    protected byte UnitId { get; }

    public override Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => throw new NotSupportedException($"{Protocol} 协议不支持原始字节发送");

    /// <summary>读保持寄存器区</summary>
    public short[] GetHoldingRegisters(int start, int count)
    {
        var server = RequireServer();
        var result = new short[count];
        lock (server.Lock)
            server.GetHoldingRegisters(UnitId).Slice(start, count).CopyTo(result);
        return result;
    }

    /// <summary>写保持寄存器区</summary>
    public void SetHoldingRegisters(int start, short[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var server = RequireServer();
        lock (server.Lock)
            values.AsSpan().CopyTo(server.GetHoldingRegisters(UnitId).Slice(start, values.Length));
    }

    /// <summary>读输入寄存器区</summary>
    public short[] GetInputRegisters(int start, int count)
    {
        var server = RequireServer();
        var result = new short[count];
        lock (server.Lock)
            server.GetInputRegisters(UnitId).Slice(start, count).CopyTo(result);
        return result;
    }

    /// <summary>写输入寄存器区</summary>
    public void SetInputRegisters(int start, short[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var server = RequireServer();
        lock (server.Lock)
            values.AsSpan().CopyTo(server.GetInputRegisters(UnitId).Slice(start, values.Length));
    }

    /// <summary>读线圈区</summary>
    public bool[] GetCoils(int start, int count)
    {
        var server = RequireServer();
        var result = new bool[count];
        lock (server.Lock)
        {
            var coils = server.GetCoils(UnitId);
            for (int i = 0; i < count; i++)
                result[i] = (coils[(start + i) / 8] & (1 << ((start + i) % 8))) != 0;
        }
        return result;
    }

    /// <summary>写线圈区</summary>
    public void SetCoils(int start, bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var server = RequireServer();
        lock (server.Lock)
        {
            var coils = server.GetCoils(UnitId);
            for (int i = 0; i < values.Length; i++)
            {
                int index = start + i;
                if (values[i])
                    coils[index / 8] |= (byte)(1 << (index % 8));
                else
                    coils[index / 8] &= (byte)~(1 << (index % 8));
            }
        }
    }

    /// <summary>读离散输入区</summary>
    public bool[] GetDiscreteInputs(int start, int count)
    {
        var server = RequireServer();
        var result = new bool[count];
        lock (server.Lock)
        {
            var inputs = server.GetDiscreteInputs(UnitId);
            for (int i = 0; i < count; i++)
                result[i] = (inputs[(start + i) / 8] & (1 << ((start + i) % 8))) != 0;
        }
        return result;
    }

    /// <summary>写离散输入区</summary>
    public void SetDiscreteInputs(int start, bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var server = RequireServer();
        lock (server.Lock)
        {
            var inputs = server.GetDiscreteInputs(UnitId);
            for (int i = 0; i < values.Length; i++)
            {
                int index = start + i;
                if (values[i])
                    inputs[index / 8] |= (byte)(1 << (index % 8));
                else
                    inputs[index / 8] &= (byte)~(1 << (index % 8));
            }
        }
    }

    protected void HookServerEvents(ModbusServer server)
    {
        server.RegistersChanged += (_, e) => RaiseLog($"主站写入寄存器（站号 {e.UnitIdentifier}）");
        server.CoilsChanged += (_, e) => RaiseLog($"主站写入线圈（站号 {e.UnitIdentifier}）");
    }

    private ModbusServer RequireServer()
        => Server ?? throw new InvalidOperationException("从站未启动");
}
