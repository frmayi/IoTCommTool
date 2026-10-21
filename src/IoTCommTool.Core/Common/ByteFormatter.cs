using System.Text;

namespace IoTCommTool.Core.Common;

/// <summary>字节数组与 Hex/ASCII 字符串互转、序号附加与解析</summary>
public static class ByteFormatter
{
    /// <summary>转 Hex 字符串，如 "AA BB CC"</summary>
    public static string ToHexString(ReadOnlySpan<byte> data, string separator = " ")
        => string.Join(separator, data.ToArray().Select(b => b.ToString("X2")));

    /// <summary>解析 Hex 字符串，支持 "AA BB CC"、"AABBCC"、"AA-BB-CC"、"0xAA" 等格式</summary>
    public static byte[] ParseHex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '0' && i + 1 < text.Length && text[i + 1] is 'x' or 'X')
            {
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c) || c is '-' or ':' or '_')
                continue;

            sb.Append(c);
        }

        string hex = sb.ToString();
        if (hex.Length == 0)
            return [];
        if (hex.Length % 2 != 0)
            throw new FormatException("十六进制字符串的字符数必须为偶数");

        return Convert.FromHexString(hex);
    }

    /// <summary>按 ASCII 显示，不可打印字符显示为 '.'</summary>
    public static string ToAsciiString(ReadOnlySpan<byte> data)
    {
        var chars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
            chars[i] = data[i] is >= 0x20 and <= 0x7E ? (char)data[i] : '.';
        return new string(chars);
    }

    /// <summary>ASCII 字符串转字节数组</summary>
    public static byte[] FromAsciiString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Encoding.ASCII.GetBytes(text);
    }

    /// <summary>
    /// 转可编辑的 ASCII 表示：可打印字符原样保留，换行/制表/反斜杠使用 \r \n \t \\，
    /// 其余字节使用 \xNN，保证 Hex ↔ ASCII 切换无信息丢失。
    /// </summary>
    public static string ToAsciiEscaped(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (byte b in data)
        {
            switch (b)
            {
                case 0x5C:
                    sb.Append("\\");
                    break;
                case 0x0D:
                    sb.Append("\r");
                    break;
                case 0x0A:
                    sb.Append("\n");
                    break;
                case 0x09:
                    sb.Append("\t");
                    break;
                default:
                    if (b is >= 0x20 and <= 0x7E)
                        sb.Append((char)b);
                    else
                        sb.Append("\\x").Append(b.ToString("X2"));
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>解析 ASCII 编辑内容，支持 \r \n \t \0 \\ 和 \xNN 转义。</summary>
    public static byte[] FromAsciiEscaped(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<byte>(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                if (next is 'x' or 'X' && i + 3 < text.Length
                    && IsHexDigit(text[i + 2]) && IsHexDigit(text[i + 3]))
                {
                    result.Add((byte)((HexValue(text[i + 2]) << 4) | HexValue(text[i + 3])));
                    i += 3;
                    continue;
                }

                switch (next)
                {
                    case 'r':
                        result.Add(0x0D);
                        i++;
                        continue;
                    case 'n':
                        result.Add(0x0A);
                        i++;
                        continue;
                    case 't':
                        result.Add(0x09);
                        i++;
                        continue;
                    case '0':
                        result.Add(0x00);
                        i++;
                        continue;
                    case '\\':
                        result.Add(0x5C);
                        i++;
                        continue;
                }
            }

            result.Add(c <= 0x7F ? (byte)c : (byte)'?');
        }

        return result.ToArray();
    }

    private static bool IsHexDigit(char c)
        => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static int HexValue(char c)
        => c is >= '0' and <= '9' ? c - '0'
            : c is >= 'a' and <= 'f' ? c - 'a' + 10
            : c - 'A' + 10;

    /// <summary>在负载前附加 4 字节大端序号，返回新数组</summary>
    public static byte[] PrependSequence(uint seq, ReadOnlySpan<byte> payload)
    {
        var result = new byte[4 + payload.Length];
        result[0] = (byte)(seq >> 24);
        result[1] = (byte)(seq >> 16);
        result[2] = (byte)(seq >> 8);
        result[3] = (byte)seq;
        payload.CopyTo(result.AsSpan(4));
        return result;
    }

    /// <summary>解析前置 4 字节大端序号；数据不足 4 字节时返回 false</summary>
    public static bool TryExtractSequence(ReadOnlySpan<byte> data, out uint sequence, out byte[] payload)
    {
        if (data.Length < 4)
        {
            sequence = 0;
            payload = [];
            return false;
        }

        sequence = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        payload = data[4..].ToArray();
        return true;
    }
}
