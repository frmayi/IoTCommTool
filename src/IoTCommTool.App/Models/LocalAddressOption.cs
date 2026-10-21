namespace IoTCommTool.App.Models;

/// <summary>One local bind choice shown in connection forms.</summary>
public sealed record LocalAddressOption(string Address, string Display)
{
    public override string ToString() => Display;
}
