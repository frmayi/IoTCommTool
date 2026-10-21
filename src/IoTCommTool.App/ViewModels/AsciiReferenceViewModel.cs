using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IoTCommTool.App.ViewModels;

public sealed record AsciiEntry(int Code, string Hex, string Char, string Description)
{
    public string Dec { get; } = Code.ToString();

    public string Escape { get; } = $"\\x{Hex}";

    public bool Matches(string filter)
        => Dec.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || Hex.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || Char.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || Description.Contains(filter, StringComparison.OrdinalIgnoreCase);
}

public partial class AsciiReferenceViewModel : ObservableObject
{
    private static readonly string[] ControlNames =
    {
        "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL",
        "BS", "TAB", "LF", "VT", "FF", "CR", "SO", "SI",
        "DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB",
        "CAN", "EM", "SUB", "ESC", "FS", "GS", "RS", "US",
    };

    private static readonly string[] ControlDescriptions =
    {
        "空字符", "标题开始", "正文开始", "正文结束", "传输结束", "询问", "确认", "响铃",
        "退格", "水平制表符", "换行", "垂直制表符", "换页", "回车", "移出", "移入",
        "数据链路转义", "设备控制 1", "设备控制 2", "设备控制 3", "设备控制 4", "否定确认", "同步空闲", "传输块结束",
        "取消", "介质结束", "替换", "转义", "文件分隔符", "组分隔符", "记录分隔符", "单元分隔符",
    };

    private static readonly IReadOnlyList<AsciiEntry> All = BuildEntries();

    public AsciiReferenceViewModel()
    {
        Entries = new ObservableCollection<AsciiEntry>(All);
    }

    public ObservableCollection<AsciiEntry> Entries { get; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    partial void OnFilterTextChanged(string value)
    {
        string filter = value.Trim();
        Entries.Clear();
        foreach (AsciiEntry entry in All)
        {
            if (filter.Length == 0 || entry.Matches(filter))
                Entries.Add(entry);
        }
    }

    private static IReadOnlyList<AsciiEntry> BuildEntries()
    {
        var entries = new List<AsciiEntry>(128);
        for (int code = 0; code < 128; code++)
        {
            string hex = code.ToString("X2");
            string ch;
            string description;
            if (code < 32)
            {
                ch = ControlNames[code];
                description = $"控制字符 · {ControlDescriptions[code]}";
            }
            else if (code == 32)
            {
                ch = "Space";
                description = "空格";
            }
            else if (code == 127)
            {
                ch = "DEL";
                description = "控制字符 · 删除";
            }
            else
            {
                ch = ((char)code).ToString();
                description = "可打印字符";
            }

            entries.Add(new AsciiEntry(code, hex, ch, description));
        }

        return entries;
    }
}
