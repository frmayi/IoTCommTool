using CommunityToolkit.Mvvm.ComponentModel;
using IoTCommTool.Core.Tcp;

namespace IoTCommTool.App.ViewModels;

public partial class SessionItem : ViewModelBase
{
    public SessionItem(ClientSession session, string name)
    {
        Session = session;
        Name = name;
    }

    public ClientSession Session { get; }

    public string Name { get; }

    public string ShortId => Session.SessionId.ToString("N")[..8];

    public string RemoteEndpoint => Session.RemoteEndpoint;

    [ObservableProperty]
    public partial int ReceivedCount { get; private set; }

    public void IncrementReceived() => ReceivedCount++;
}
