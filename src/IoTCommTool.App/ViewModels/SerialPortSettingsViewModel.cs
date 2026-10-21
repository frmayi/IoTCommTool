using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Services;

namespace IoTCommTool.App.ViewModels;

public sealed record SerialSettingsSnapshot(string PortName, int BaudRate, int DataBits, Parity Parity, StopBits StopBits);

public partial class SerialPortSettingsViewModel : ViewModelBase
{
    public SerialPortSettingsViewModel() => RefreshPorts();

    public ObservableCollection<string> PortNames { get; } = new();

    [ObservableProperty]
    public partial string? SelectedPortName { get; set; }

    [ObservableProperty]
    public partial string BaudRate { get; set; } = "9600";

    public IReadOnlyList<string> DataBitsOptions { get; } = new[] { "8", "7" };

    [ObservableProperty]
    public partial int DataBitsIndex { get; set; }

    public IReadOnlyList<string> ParityOptions { get; } = new[] { "无", "奇校验", "偶校验" };

    [ObservableProperty]
    public partial int ParityIndex { get; set; }

    public IReadOnlyList<string> StopBitsOptions { get; } = new[] { "1", "1.5", "2" };

    [ObservableProperty]
    public partial int StopBitsIndex { get; set; }

    public int DataBits => DataBitsIndex == 1 ? 7 : 8;

    public string ParityName => MapParity(ParityIndex).ToString();

    public string StopBitsName => MapStopBits(StopBitsIndex).ToString();

    [RelayCommand]
    private void RefreshPorts()
    {
        string? previous = SelectedPortName;
        PortNames.Clear();
        foreach (string name in SerialPort.GetPortNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            PortNames.Add(name);
        SelectedPortName = previous is not null && PortNames.Contains(previous)
            ? previous
            : PortNames.FirstOrDefault();
    }

    public SerialSettingsSnapshot Snapshot()
    {
        string? portName = SelectedPortName;
        if (string.IsNullOrWhiteSpace(portName))
            throw new FormatException("请选择串口（可点击“刷新”枚举本机串口）");
        return new SerialSettingsSnapshot(
            portName,
            InputParser.ParseInt(BaudRate, 300, 921600, "波特率"),
            DataBits,
            MapParity(ParityIndex),
            MapStopBits(StopBitsIndex));
    }

    public void Load(string? portName, int baudRate, int dataBits, string? parity, string? stopBits)
    {
        if (!string.IsNullOrWhiteSpace(portName))
        {
            if (!PortNames.Contains(portName))
                PortNames.Add(portName);
            SelectedPortName = portName;
        }

        BaudRate = baudRate.ToString();
        DataBitsIndex = dataBits == 7 ? 1 : 0;
        ParityIndex = Enum.TryParse(parity, out Parity p)
            ? p switch { Parity.Odd => 1, Parity.Even => 2, _ => 0 }
            : 0;
        StopBitsIndex = Enum.TryParse(stopBits, out StopBits s)
            ? s switch { StopBits.OnePointFive => 1, StopBits.Two => 2, _ => 0 }
            : 0;
    }

    private static Parity MapParity(int index) => index switch
    {
        1 => Parity.Odd,
        2 => Parity.Even,
        _ => Parity.None,
    };

    private static StopBits MapStopBits(int index) => index switch
    {
        1 => StopBits.OnePointFive,
        2 => StopBits.Two,
        _ => StopBits.One,
    };
}
