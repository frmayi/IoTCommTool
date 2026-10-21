namespace IoTCommTool.Core.Abstractions;

/// <summary>通信协议类型</summary>
public enum ProtocolType
{
    TcpClient,
    TcpServer,
    Udp,
    ModbusTcpMaster,
    ModbusTcpSlave,
    ModbusRtuMaster,
    ModbusRtuSlave,
    S7
}
