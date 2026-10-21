using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using IoTCommTool.App.Models;

namespace IoTCommTool.App.Services;

/// <summary>
/// Snapshot of local NIC MAC addresses for registration packets. MACs are stable,
/// so callers query once instead of tracking network changes.
/// </summary>
public static class LocalMacAddressService
{
    public static IReadOnlyList<LocalMacOption> GetSnapshot()
    {
        var result = new List<LocalMacOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                byte[] bytes = ni.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length != 6)
                    continue;

                string mac = string.Join(':', bytes.Select(b => b.ToString("X2")));
                if (!seen.Add(mac))
                    continue;

                string name = string.IsNullOrWhiteSpace(ni.Name) ? ni.Description : ni.Name;
                result.Add(new LocalMacOption(mac, $"{name} · {mac}"));
            }
        }
        catch
        {
            // A missing/partially initialized network stack should not break the UI.
        }

        return result;
    }
}
