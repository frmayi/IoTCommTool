using System;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public sealed class SavedConfigItem
{
    public SavedConfigItem(ConnectionConfigEntity entity, Action<SavedConfigItem> open, Action<SavedConfigItem> delete)
    {
        Entity = entity;
        OpenCommand = new RelayCommand(() => open(this));
        DeleteCommand = new RelayCommand(() => delete(this));
        EndpointSummary = BuildEndpointSummary(Entity.ParamsJson);
        ProtocolGroup = ProtocolItem.GetGroup(Entity.Protocol);
        LastUsedRelativeText = FormatRelative(Entity.LastUsedAt);
    }

    public ConnectionConfigEntity Entity { get; }

    public string DisplayName => Entity.DisplayName;

    public string ProtocolName => ProtocolItem.GetName(Entity.Protocol);

    public string ProtocolCode => ProtocolItem.GetCode(Entity.Protocol);

    public string EndpointSummary { get; }

    public string ProtocolGroup { get; }

    public string LastUsedRelativeText { get; }

    public string LastUsedText
        => DateTime.TryParse(Entity.LastUsedAt, out DateTime t) ? t.ToString("MM-dd HH:mm") : Entity.LastUsedAt;

    public ICommand OpenCommand { get; }

    public ICommand DeleteCommand { get; }

    private static string FormatRelative(string? value)
    {
        if (!DateTime.TryParse(value, out DateTime time))
            return value ?? string.Empty;

        DateTime today = DateTime.Today;
        if (time.Date == today)
            return $"今天 {time:HH:mm}";
        if (time.Date == today.AddDays(-1))
            return $"昨天 {time:HH:mm}";
        return time.Year == today.Year ? time.ToString("MM-dd HH:mm") : time.ToString("yyyy-MM-dd HH:mm");
    }

    private static string BuildEndpointSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string? host = GetText(root, "Host");
            bool hasPort = TryGetInt(root, "Port", out int port);
            if (!string.IsNullOrWhiteSpace(host) && hasPort)
                return $"{host}:{port}";

            string? remoteHost = GetText(root, "RemoteHost");
            if (!string.IsNullOrWhiteSpace(remoteHost))
            {
                string remote = TryGetInt(root, "RemotePort", out int remotePort)
                    ? $"{remoteHost}:{remotePort}"
                    : remoteHost;
                return TryGetInt(root, "LocalPort", out int localPort)
                    ? $"{localPort} → {remote}"
                    : remote;
            }

            string? portName = GetText(root, "PortName");
            if (!string.IsNullOrWhiteSpace(portName))
            {
                return TryGetInt(root, "BaudRate", out int baudRate)
                    ? $"串口 {portName} {baudRate}"
                    : $"串口 {portName}";
            }

            if (TryGetInt(root, "LocalPort", out int udpLocalPort))
                return $"UDP :{udpLocalPort}";
            if (hasPort)
                return $"端口 {port}";
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetText(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement element))
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => null,
            };
        }
        return null;
    }

    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement element))
        {
            if (element.ValueKind == JsonValueKind.Number)
                return element.TryGetInt32(out value);
            if (element.ValueKind == JsonValueKind.String)
                return int.TryParse(element.GetString(), out value);
        }
        return false;
    }
}
