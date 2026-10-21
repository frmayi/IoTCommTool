using System;
using System.Globalization;
using System.Linq;

namespace IoTCommTool.App.Services;

public static class InputParser
{
    public static int ParseInt(string? text, int min, int max, string name)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
            throw new FormatException($"{name}无效：请输入 {min} ~ {max} 之间的整数");
        return value;
    }

    public static int ParsePort(string? text, string name = "端口")
        => ParseInt(text, 1, 65535, name);

    public static byte ParseByte(string? text, string name)
        => (byte)ParseInt(text, 0, 255, name);

    public static short ParseShort(string? text, string name)
    {
        if (!short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short value))
            throw new FormatException($"{name}无效：请输入 -32768 ~ 32767 之间的整数");
        return value;
    }

    public static float ParseFloat(string? text, string name)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            throw new FormatException($"{name}无效：请输入数字");
        return value;
    }

    public static bool ParseBool(string? text, string name)
    {
        string? t = text?.Trim();
        if (string.Equals(t, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(t, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase))
            return false;
        throw new FormatException($"{name}无效：请输入 0/1 或 true/false");
    }

    public static short[] ParseShortList(string? text, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException($"{name}不能为空（多个值用英文逗号分隔）");
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => ParseShort(p, name))
            .ToArray();
    }

    public static bool[] ParseBoolList(string? text, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException($"{name}不能为空（多个值用英文逗号分隔）");
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => ParseBool(p, name))
            .ToArray();
    }

    public static string RequireHost(string? text)
    {
        string? host = text?.Trim();
        return string.IsNullOrEmpty(host) ? throw new FormatException("主机不能为空") : host;
    }
}
