using IoTCommTool.Core.Abstractions;
using S7.Net;
using S7Boolean = S7.Net.Types.Boolean;
using S7DInt = S7.Net.Types.DInt;
using S7Int = S7.Net.Types.Int;
using S7Real = S7.Net.Types.Real;
using S7String = S7.Net.Types.S7String;

namespace IoTCommTool.Core.S7;

/// <summary>西门子 S7 PLC 连接（基于 S7.Net）</summary>
public sealed class S7Connection : ConnectionBase
{
    private readonly CpuType _cpu;
    private readonly string _host;
    private readonly int _port;
    private readonly short _rack;
    private readonly short _slot;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    private Plc? _plc;

    public S7Connection(CpuType cpu, string host, int port = 102, short rack = 0, short slot = 1)
    {
        _cpu = cpu;
        _host = host;
        _port = port;
        _rack = rack;
        _slot = slot;
        DisplayName = $"S7 {cpu} {host}:{port}";
    }

    public override ProtocolType Protocol => ProtocolType.S7;

    public string Host => _host;

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
            return;

        SetState(ConnectionState.Connecting);
        var plc = new Plc(_cpu, _host, _port, _rack, _slot);
        try
        {
            await plc.OpenAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { plc.Close(); } catch { }
            SetState(ConnectionState.Faulted, ex.Message);
            RaiseLog($"连接 PLC {_host}:{_port} 失败: {ex.Message}", LogLevel.Error);
            throw;
        }

        _plc = plc;
        SetState(ConnectionState.Connected);
        RaiseLog($"已连接 PLC {_host}:{_port}（{_cpu}，机架 {_rack}，插槽 {_slot}）");
    }

    public override Task CloseAsync()
    {
        var plc = _plc;
        _plc = null;

        if (plc is not null)
        {
            try { plc.Close(); } catch { }
        }

        if (State != ConnectionState.Disconnected)
        {
            SetState(ConnectionState.Disconnected, "已关闭");
            RaiseLog("连接已关闭");
        }

        return Task.CompletedTask;
    }

    public override Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => throw new NotSupportedException("S7 协议不支持原始字节发送");

    /// <summary>按字节读取存储区</summary>
    public Task<byte[]> ReadBytesAsync(DataType area, int db, int start, int count, CancellationToken ct = default)
        => ExecAsync((p, t) => p.ReadBytesAsync(area, db, start, count, t), "读取字节", ct);

    /// <summary>按字节写入存储区</summary>
    public Task WriteBytesAsync(DataType area, int db, int start, byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return ExecAsync((p, t) => p.WriteBytesAsync(area, db, start, data, t), "写入字节", ct);
    }

    /// <summary>读 16 位有符号整数（2 字节）</summary>
    public async Task<short> ReadShortAsync(DataType area, int db, int start, CancellationToken ct = default)
        => S7Int.FromByteArray(await ReadBytesAsync(area, db, start, 2, ct).ConfigureAwait(false));

    /// <summary>写 16 位有符号整数</summary>
    public Task WriteShortAsync(DataType area, int db, int start, short value, CancellationToken ct = default)
        => WriteBytesAsync(area, db, start, S7Int.ToByteArray(value), ct);

    /// <summary>读 32 位有符号整数（4 字节）</summary>
    public async Task<int> ReadIntAsync(DataType area, int db, int start, CancellationToken ct = default)
        => S7DInt.FromByteArray(await ReadBytesAsync(area, db, start, 4, ct).ConfigureAwait(false));

    /// <summary>写 32 位有符号整数</summary>
    public Task WriteIntAsync(DataType area, int db, int start, int value, CancellationToken ct = default)
        => WriteBytesAsync(area, db, start, S7DInt.ToByteArray(value), ct);

    /// <summary>读单精度浮点数（4 字节）</summary>
    public async Task<float> ReadFloatAsync(DataType area, int db, int start, CancellationToken ct = default)
        => S7Real.FromByteArray(await ReadBytesAsync(area, db, start, 4, ct).ConfigureAwait(false));

    /// <summary>写单精度浮点数</summary>
    public Task WriteFloatAsync(DataType area, int db, int start, float value, CancellationToken ct = default)
        => WriteBytesAsync(area, db, start, S7Real.ToByteArray(value), ct);

    /// <summary>读布尔量（按位）</summary>
    public async Task<bool> ReadBoolAsync(DataType area, int db, int start, int bit, CancellationToken ct = default)
        => S7Boolean.GetValue((await ReadBytesAsync(area, db, start, 1, ct).ConfigureAwait(false))[0], bit);

    /// <summary>写布尔量（按位）</summary>
    public Task WriteBoolAsync(DataType area, int db, int start, int bit, bool value, CancellationToken ct = default)
        => ExecAsync((p, t) => p.WriteBitAsync(area, db, start, bit, value, t), "写入位", ct);

    /// <summary>读 S7 String（含 2 字节头，maxLength 为声明的最大长度）</summary>
    public async Task<string> ReadStringAsync(DataType area, int db, int start, int maxLength, CancellationToken ct = default)
        => S7String.FromByteArray(await ReadBytesAsync(area, db, start, maxLength + 2, ct).ConfigureAwait(false));

    /// <summary>写 S7 String（maxLength 为声明的最大长度）</summary>
    public Task WriteStringAsync(DataType area, int db, int start, int maxLength, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteBytesAsync(area, db, start, S7String.ToByteArray(value, maxLength), ct);
    }

    private async Task<T> ExecAsync<T>(Func<Plc, CancellationToken, Task<T>> op, string opName, CancellationToken ct)
    {
        var plc = _plc ?? throw new InvalidOperationException("未连接");

        await _opLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await op(plc, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseLog($"{opName}失败: {ex.Message}", LogLevel.Error);
            if (!plc.IsConnected)
                SetState(ConnectionState.Faulted, ex.Message);
            throw;
        }
        finally
        {
            _opLock.Release();
        }
    }

    private async Task ExecAsync(Func<Plc, CancellationToken, Task> op, string opName, CancellationToken ct)
    {
        var plc = _plc ?? throw new InvalidOperationException("未连接");

        await _opLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await op(plc, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseLog($"{opName}失败: {ex.Message}", LogLevel.Error);
            if (!plc.IsConnected)
                SetState(ConnectionState.Faulted, ex.Message);
            throw;
        }
        finally
        {
            _opLock.Release();
        }
    }
}
