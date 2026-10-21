namespace IoTCommTool.App.Models;

public sealed record TcpClientParams
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 502;

    /// <summary>Optional local source address; null/empty means OS routing.</summary>
    public string? LocalBindHost { get; set; }

    public bool RegisterEnabled { get; set; }

    public string? RegisterText { get; set; }

    public bool RegisterHex { get; set; } = true;
}

public sealed record TcpServerParams
{
    public string Host { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 5000;
}

public sealed record UdpParams
{
    public int LocalPort { get; set; } = 5001;

    public string? RemoteHost { get; set; }

    public int? RemotePort { get; set; }

    public bool RegisterEnabled { get; set; }

    public string? RegisterText { get; set; }

    public bool RegisterHex { get; set; } = true;
}

public sealed record ModbusTcpMasterParams
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 502;
}

public sealed record ModbusTcpSlaveParams
{
    public int Port { get; set; } = 502;

    public int UnitId { get; set; } = 1;
}

public sealed record ModbusRtuMasterParams
{
    public string PortName { get; set; } = string.Empty;

    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public string Parity { get; set; } = "None";

    public string StopBits { get; set; } = "One";
}

public sealed record ModbusRtuSlaveParams
{
    public string PortName { get; set; } = string.Empty;

    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public string Parity { get; set; } = "None";

    public string StopBits { get; set; } = "One";

    public int UnitId { get; set; } = 1;
}

public sealed record S7Params
{
    public string Cpu { get; set; } = "S71200";

    public string Host { get; set; } = "192.168.0.1";

    public int Port { get; set; } = 102;

    public int Rack { get; set; }

    public int Slot { get; set; } = 1;
}
