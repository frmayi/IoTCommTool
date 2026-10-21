using System;
using System.Text;

namespace IoTCommTool.App.ViewModels;

/// <summary>当前标签页内可回填的发送历史。</summary>
public sealed class SendHistoryItem
{
    public SendHistoryItem(string inputText, bool isHex, bool appendCrLf, bool appendCrc16, bool appendSequence)
    {
        InputText = inputText;
        IsHex = isHex;
        AppendCrLf = appendCrLf;
        AppendCrc16 = appendCrc16;
        AppendSequence = appendSequence;
        Preview = BuildPreview(inputText);
        Meta = BuildMeta(isHex, appendCrLf, appendCrc16, appendSequence);
    }

    public string InputText { get; }

    public bool IsHex { get; }

    public bool AppendCrLf { get; }

    public bool AppendCrc16 { get; }

    public bool AppendSequence { get; }

    public string Preview { get; }

    public string Meta { get; }

    private static string BuildPreview(string text)
    {
        string flat = text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();

        while (flat.Contains("  ", StringComparison.Ordinal))
            flat = flat.Replace("  ", " ", StringComparison.Ordinal);

        return flat.Length <= 36 ? flat : flat[..35] + "…";
    }

    private static string BuildMeta(bool isHex, bool appendCrLf, bool appendCrc16, bool appendSequence)
    {
        var sb = new StringBuilder(isHex ? "HEX" : "ASCII");
        if (appendCrLf)
            sb.Append(" · CRLF");
        if (appendCrc16)
            sb.Append(" · CRC16");
        if (appendSequence)
            sb.Append(" · 序号");
        return sb.ToString();
    }
}
