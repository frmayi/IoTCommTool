using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IoTCommTool.Core.Abstractions;

namespace IoTCommTool.App.ViewModels;

public sealed class ProtocolItem
{
    private sealed record Descriptor(string Name, string Code, string Description, string Group);

    private static readonly IReadOnlyDictionary<ProtocolType, Descriptor> Descriptors =
        new Dictionary<ProtocolType, Descriptor>
        {
            [ProtocolType.TcpClient] = new("TCP 客户端", "TCP", "主动连接远程 TCP 服务端", "基础通信"),
            [ProtocolType.TcpServer] = new("TCP 服务端", "TS", "监听端口，接受多个客户端会话", "基础通信"),
            [ProtocolType.Udp] = new("UDP", "UDP", "绑定本地端口，收发数据报", "基础通信"),
            [ProtocolType.ModbusTcpMaster] = new("Modbus TCP 主站", "MT", "读写远程 Modbus TCP 从站", "Modbus"),
            [ProtocolType.ModbusTcpSlave] = new("Modbus TCP 从站", "MS", "模拟 Modbus TCP 从站寄存器", "Modbus"),
            [ProtocolType.ModbusRtuMaster] = new("Modbus RTU 主站", "RM", "通过串口读写 Modbus RTU 从站", "Modbus"),
            [ProtocolType.ModbusRtuSlave] = new("Modbus RTU 从站", "RS", "通过串口模拟 Modbus RTU 从站", "Modbus"),
            [ProtocolType.S7] = new("S7", "S7", "连接西门子 S7-300/400/1200/1500", "PLC"),
        };

    public ProtocolItem(ProtocolType? type, string name)
    {
        Type = type;
        Name = name;
        ShortCode = type is null ? "ALL" : GetDescriptor(type.Value).Code;
        Description = type is null ? "全部协议" : GetDescriptor(type.Value).Description;
        Group = type is null ? "全部" : GetDescriptor(type.Value).Group;
    }

    public ProtocolType? Type { get; }

    public string Name { get; }

    public string ShortCode { get; }

    public string Description { get; }

    public string Group { get; }

    public static IReadOnlyList<ProtocolItem> All { get; } =
        new ReadOnlyCollection<ProtocolItem>(
            Descriptors.Select(p => new ProtocolItem(p.Key, p.Value.Name)).ToList());

    public static string GetName(ProtocolType type)
        => Descriptors.TryGetValue(type, out Descriptor? descriptor) ? descriptor.Name : type.ToString();

    public static string GetName(string? protocol)
        => Enum.TryParse(protocol, out ProtocolType type) ? GetName(type) : protocol ?? string.Empty;

    public static string GetGroup(string? protocol)
        => Enum.TryParse(protocol, out ProtocolType type) && Descriptors.TryGetValue(type, out Descriptor? descriptor)
            ? descriptor.Group
            : string.Empty;

    public static string GetCode(string? protocol)
        => Enum.TryParse(protocol, out ProtocolType type) && Descriptors.TryGetValue(type, out Descriptor? descriptor)
            ? descriptor.Code
            : "?";

    private static Descriptor GetDescriptor(ProtocolType type)
        => Descriptors.TryGetValue(type, out Descriptor? descriptor)
            ? descriptor
            : new Descriptor(type.ToString(), "?", string.Empty, "其他");
}
