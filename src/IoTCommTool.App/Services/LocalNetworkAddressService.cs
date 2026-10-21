using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using IoTCommTool.App.Models;

namespace IoTCommTool.App.Services;

/// <summary>
/// App-wide snapshot of active IPv4 addresses. Network changes only refresh
/// the choices; they never rebind or close an existing connection.
/// </summary>
public sealed class LocalNetworkAddressService
{
    public static LocalNetworkAddressService Instance { get; } = new();

    private readonly object _gate = new();
    private List<LocalAddressOption> _snapshot;
    private System.Threading.Timer? _refreshTimer;

    private LocalNetworkAddressService()
    {
        _snapshot = BuildSnapshot();
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    /// <summary>Raised on a background thread after the address list actually changes.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<LocalAddressOption> GetSnapshot()
    {
        lock (_gate)
            return _snapshot.ToArray();
    }

    public void Refresh()
    {
        List<LocalAddressOption> next = BuildSnapshot();
        bool changed;
        lock (_gate)
        {
            changed = !_snapshot.SequenceEqual(next);
            if (changed)
                _snapshot = next;
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => ScheduleRefresh();

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        lock (_gate)
        {
            _refreshTimer ??= new System.Threading.Timer(
                _ => Refresh(),
                null,
                Timeout.Infinite,
                Timeout.Infinite);
            _refreshTimer.Change(300, Timeout.Infinite);
        }
    }

    private static List<LocalAddressOption> BuildSnapshot()
    {
        var result = new List<LocalAddressOption>
        {
            new("0.0.0.0", "所有网卡 (0.0.0.0)"),
        };

        var addresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                string interfaceName = string.IsNullOrWhiteSpace(ni.Name) ? ni.Description : ni.Name;
                foreach (UnicastIPAddressInformation address in ni.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork
                        || IPAddress.IsLoopback(address.Address))
                        continue;

                    string ip = address.Address.ToString();
                    if (!addresses.ContainsKey(ip))
                        addresses[ip] = interfaceName;
                }
            }
        }
        catch
        {
            // A missing/partially initialized network stack should not break the UI.
        }

        List<string> ordered = addresses.Keys.ToList();
        ordered.Sort(CompareAddress);
        foreach (string ip in ordered)
            result.Add(new LocalAddressOption(ip, $"{addresses[ip]} · {ip}"));

        result.Add(new LocalAddressOption("127.0.0.1", "本机回环 · 127.0.0.1"));
        return result;
    }

    private static int CompareAddress(string a, string b)
    {
        byte[] x = IPAddress.Parse(a).GetAddressBytes();
        byte[] y = IPAddress.Parse(b).GetAddressBytes();
        for (int i = 0; i < x.Length && i < y.Length; i++)
        {
            int comparison = x[i].CompareTo(y[i]);
            if (comparison != 0)
                return comparison;
        }

        return x.Length.CompareTo(y.Length);
    }
}
