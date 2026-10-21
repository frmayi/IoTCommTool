using System;

namespace IoTCommTool.Core.Common;

/// <summary>组合原始发送帧：可选 CRLF、4 字节序号、Modbus CRC16。</summary>
public static class FrameComposer
{
    /// <summary>
    /// 按 UI 约定的顺序组帧：负载 → CRLF → 4 字节大端序号 → CRC16（低字节在前）。
    /// </summary>
    public static byte[] Compose(
        ReadOnlySpan<byte> payload,
        bool appendCrLf = false,
        bool appendSequence = false,
        uint sequence = 0,
        bool appendCrc16 = false)
    {
        byte[] result = payload.ToArray();

        if (appendCrLf)
        {
            Array.Resize(ref result, result.Length + 2);
            result[^2] = 0x0D;
            result[^1] = 0x0A;
        }

        if (appendSequence)
            result = ByteFormatter.PrependSequence(sequence, result);

        if (appendCrc16)
            result = Crc16Modbus.AppendCrc(result);

        return result;
    }
}
