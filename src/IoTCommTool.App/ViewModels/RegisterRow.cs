using CommunityToolkit.Mvvm.ComponentModel;

namespace IoTCommTool.App.ViewModels;

public partial class RegisterRow : ViewModelBase
{
    public RegisterRow(int address, string value)
    {
        Address = address;
        Value = value;
    }

    public int Address { get; }

    [ObservableProperty]
    public partial string Value { get; set; }
}
