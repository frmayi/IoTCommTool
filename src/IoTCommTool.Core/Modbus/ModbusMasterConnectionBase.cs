using FluentModbus;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.Core.Modbus;

/// <summary>Modbus 主站基类，统一读写操作（串行执行）</summary>
public abstract class ModbusMasterConnectionBase : ConnectionBase
{
    private readonly SemaphoreSlim _opLock = new(1, 1);

    protected ModbusClient? Client { get; set; }

    public override Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => throw new NotSupportedException($"{Protocol} 协议不支持原始字节发送");

    /// <summary>读保持寄存器（功能码 03）</summary>
    public Task<short[]> ReadHoldingRegistersAsync(byte unitId, int start, int count, CancellationToken ct = default)
        => ExecAsync(async (c, t) => (await c.ReadHoldingRegistersAsync<short>(unitId, start, count, t).ConfigureAwait(false)).ToArray(), "读保持寄存器", ct);

    /// <summary>读输入寄存器（功能码 04）</summary>
    public Task<short[]> ReadInputRegistersAsync(byte unitId, int start, int count, CancellationToken ct = default)
        => ExecAsync(async (c, t) => (await c.ReadInputRegistersAsync<short>(unitId, start, count, t).ConfigureAwait(false)).ToArray(), "读输入寄存器", ct);

    /// <summary>读线圈（功能码 01）</summary>
    public Task<bool[]> ReadCoilsAsync(byte unitId, int start, int count, CancellationToken ct = default)
        => ExecAsync(async (c, t) => UnpackBits((await c.ReadCoilsAsync(unitId, start, count, t).ConfigureAwait(false)).Span, count), "读线圈", ct);

    /// <summary>读离散输入（功能码 02）</summary>
    public Task<bool[]> ReadDiscreteInputsAsync(byte unitId, int start, int count, CancellationToken ct = default)
        => ExecAsync(async (c, t) => UnpackBits((await c.ReadDiscreteInputsAsync(unitId, start, count, t).ConfigureAwait(false)).Span, count), "读离散输入", ct);

    /// <summary>写单个寄存器（功能码 06）</summary>
    public Task WriteSingleRegisterAsync(byte unitId, int addr, short value, CancellationToken ct = default)
        => ExecAsync((c, t) => c.WriteSingleRegisterAsync(unitId, addr, value, t), "写单个寄存器", ct);

    /// <summary>写多个寄存器（功能码 16）</summary>
    public Task WriteMultipleRegistersAsync(byte unitId, int start, short[] values, CancellationToken ct = default)
        => ExecAsync((c, t) => c.WriteMultipleRegistersAsync(unitId, start, values, t), "写多个寄存器", ct);

    /// <summary>写单个线圈（功能码 05）</summary>
    public Task WriteSingleCoilAsync(byte unitId, int addr, bool value, CancellationToken ct = default)
        => ExecAsync((c, t) => c.WriteSingleCoilAsync(unitId, addr, value, t), "写单个线圈", ct);

    /// <summary>写多个线圈（功能码 15）</summary>
    public Task WriteMultipleCoilsAsync(byte unitId, int start, bool[] values, CancellationToken ct = default)
        => ExecAsync((c, t) => c.WriteMultipleCoilsAsync(unitId, start, values, t), "写多个线圈", ct);

    private async Task<T> ExecAsync<T>(Func<ModbusClient, CancellationToken, Task<T>> op, string opName, CancellationToken ct)
    {
        var client = Client ?? throw new InvalidOperationException("未连接");

        await _opLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await op(client, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseLog($"{opName}失败: {ex.Message}", LogLevel.Error);
            if (!client.IsConnected)
                SetState(ConnectionState.Faulted, ex.Message);
            throw;
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async Task ExecAsync(Func<ModbusClient, CancellationToken, Task> op, string opName, CancellationToken ct)
    {
        var client = Client ?? throw new InvalidOperationException("未连接");

        await _opLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await op(client, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseLog($"{opName}失败: {ex.Message}", LogLevel.Error);
            if (!client.IsConnected)
                SetState(ConnectionState.Faulted, ex.Message);
            throw;
        }
        finally
        {
            _opLock.Release();
        }
    }

    private static bool[] UnpackBits(ReadOnlySpan<byte> data, int count)
    {
        var result = new bool[count];
        for (int i = 0; i < count; i++)
            result[i] = (data[i / 8] & (1 << (i % 8))) != 0;
        return result;
    }
}
