namespace IoTCommTool.Core.Common;

/// <summary>Modbus RTU 使用的 CRC16（多项式 0xA001，初值 0xFFFF）</summary>
public static class Crc16Modbus
{
    /// <summary>计算 CRC16，返回值为完整 16 位校验码</summary>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }

        return crc;
    }

    /// <summary>在帧尾追加 CRC（低字节在前），返回新数组</summary>
    public static byte[] AppendCrc(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        ushort crc = Compute(frame);
        var result = new byte[frame.Length + 2];
        frame.CopyTo(result, 0);
        result[^2] = (byte)(crc & 0xFF);
        result[^1] = (byte)(crc >> 8);
        return result;
    }

    /// <summary>校验带 CRC 的帧（最后两字节为低字节在前的 CRC）</summary>
    public static bool Verify(ReadOnlySpan<byte> frameWithCrc)
    {
        if (frameWithCrc.Length < 3)
            return false;

        ushort expected = Compute(frameWithCrc[..^2]);
        return frameWithCrc[^2] == (byte)(expected & 0xFF)
            && frameWithCrc[^1] == (byte)(expected >> 8);
    }
}
