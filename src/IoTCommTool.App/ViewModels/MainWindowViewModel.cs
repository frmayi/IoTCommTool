using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTCommTool.App.Services;
using IoTCommTool.Core.Abstractions;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int DeleteUndoSeconds = 5;

    private readonly ConnectionConfigRepository _repository;
    private readonly HashSet<long> _deletingIds = new();
    private CancellationTokenSource? _deleteCts;
    private SavedConfigItem? _pendingDelete;
    private int _pendingDeleteIndex = -1;

    public MainWindowViewModel(ConnectionConfigRepository repository)
    {
        _repository = repository;
        HistoryGroups = new[] { "全部", "基础通信", "Modbus", "PLC" };
        Tabs.CollectionChanged += (_, _) =>
        {
            HasTabs = Tabs.Count > 0;
            OnPropertyChanged(nameof(TabCount));
        };
        SelectedProtocol = ProtocolItem.All[0];
        SelectedHistoryGroup = HistoryGroups[0];
        _ = RefreshSavedConfigsAsync();
    }

    public IReadOnlyList<ProtocolItem> Protocols => ProtocolItem.All;

    public IReadOnlyList<string> HistoryGroups { get; }

    public ObservableCollection<SavedConfigItem> AllSavedConfigs { get; } = new();

    /// <summary>按搜索词和协议分组过滤后的可见历史。</summary>
    public ObservableCollection<SavedConfigItem> SavedConfigs { get; } = new();

    public ObservableCollection<ConnectionViewModelBase> Tabs { get; } = new();

    public int TabCount => Tabs.Count;

    public bool HasSavedConfigs => AllSavedConfigs.Count > 0;

    public bool HasActiveHistoryFilter
        => !string.IsNullOrWhiteSpace(HistorySearchText)
            || !string.Equals(SelectedHistoryGroup, "全部", StringComparison.Ordinal);

    public bool ShowNoHistory => AllSavedConfigs.Count == 0;

    public bool ShowNoMatches => AllSavedConfigs.Count > 0 && SavedConfigs.Count == 0;

    public bool IsThemeSystem => SelectedThemeOption == ThemeManager.SystemLabel;

    public bool IsThemeLight => SelectedThemeOption == ThemeManager.LightLabel;

    public bool IsThemeDark => SelectedThemeOption == ThemeManager.DarkLabel;

    public string ThemeToolTip => $"界面主题：{SelectedThemeOption}";

    public string HistoryCountText => HasActiveHistoryFilter
        ? $"{SavedConfigs.Count} / {AllSavedConfigs.Count} 条"
        : $"{AllSavedConfigs.Count} 条";

    [ObservableProperty]
    public partial ProtocolItem? SelectedProtocol { get; set; }

    [ObservableProperty]
    public partial ConnectionViewModelBase? SelectedTab { get; set; }

    [ObservableProperty]
    public partial bool HasTabs { get; private set; }

    [ObservableProperty]
    public partial bool HistorySidebarVisible { get; set; } = true;

    [ObservableProperty]
    public partial string HistorySearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedHistoryGroup { get; set; } = "全部";

    [ObservableProperty]
    public partial string SelectedThemeOption { get; set; } = ThemeManager.CurrentMode;

    [ObservableProperty]
    public partial bool HasFilteredSavedConfigs { get; private set; }

    [ObservableProperty]
    public partial bool IsUndoVisible { get; private set; }

    partial void OnHistorySearchTextChanged(string value) => ApplyHistoryFilter();

    partial void OnSelectedHistoryGroupChanged(string value) => ApplyHistoryFilter();

    partial void OnSelectedThemeOptionChanged(string value)
    {
        ThemeManager.Apply(value);
        ThemeManager.Save(value);
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(ThemeToolTip));
    }

    [RelayCommand]
    private void SetTheme(string? mode)
    {
        if (mode is ThemeManager.SystemLabel or ThemeManager.LightLabel or ThemeManager.DarkLabel)
            SelectedThemeOption = mode;
    }

    partial void OnSelectedTabChanged(ConnectionViewModelBase? value)
    {
        if (value is not null)
            SelectedProtocol = Protocols.FirstOrDefault(p => p.Type == value.Protocol) ?? SelectedProtocol;
    }

    [RelayCommand]
    private void NewConnection()
    {
        if (SelectedProtocol?.Type is ProtocolType protocol)
            AddTab(protocol, null);
    }

    [RelayCommand]
    private void QuickStart(ProtocolItem? item)
    {
        if (item?.Type is not ProtocolType protocol)
            return;

        SelectedProtocol = item;
        AddTab(protocol, null);
    }

    [RelayCommand]
    private void ToggleHistorySidebar() => HistorySidebarVisible = !HistorySidebarVisible;

    public void ShowHistorySidebar() => HistorySidebarVisible = true;

    [RelayCommand]
    private void ClearHistoryFilters()
    {
        HistorySearchText = string.Empty;
        SelectedHistoryGroup = "全部";
    }

    [RelayCommand]
    private void CloseSelectedTab() => SelectedTab?.CloseRequestCommand.Execute(null);

    public void Shutdown()
    {
        _deleteCts?.Cancel();
        _deleteCts?.Dispose();
        _deleteCts = null;
        _pendingDelete = null;

        ConnectionViewModelBase[] tabs = Tabs.ToArray();
        Tabs.Clear();
        SelectedTab = null;
        foreach (ConnectionViewModelBase tab in tabs)
        {
            UnwireTab(tab);
            _ = DisposeTabAsync(tab);
        }
    }

    private void AddTab(ProtocolType protocol, string? paramsJson, string? displayName = null)
    {
        SelectedProtocol = Protocols.FirstOrDefault(p => p.Type == protocol) ?? SelectedProtocol;
        ConnectionViewModelBase vm = CreateTabViewModel(protocol);
        WireTab(vm);
        if (!string.IsNullOrEmpty(paramsJson))
            vm.LoadParamsJson(paramsJson);
        if (!string.IsNullOrWhiteSpace(displayName))
            vm.Title = displayName;
        Tabs.Add(vm);
        SelectedTab = vm;
    }

    private ConnectionViewModelBase CreateTabViewModel(ProtocolType protocol)
    {
        void SavedChanged() => _ = RefreshSavedConfigsAsync();
        return protocol switch
        {
            ProtocolType.TcpClient => new TcpClientViewModel(_repository, SavedChanged),
            ProtocolType.TcpServer => new TcpServerViewModel(_repository, SavedChanged),
            ProtocolType.Udp => new UdpViewModel(_repository, SavedChanged),
            ProtocolType.ModbusTcpMaster => new ModbusTcpMasterViewModel(_repository, SavedChanged),
            ProtocolType.ModbusTcpSlave => new ModbusTcpSlaveViewModel(_repository, SavedChanged),
            ProtocolType.ModbusRtuMaster => new ModbusRtuMasterViewModel(_repository, SavedChanged),
            ProtocolType.ModbusRtuSlave => new ModbusRtuSlaveViewModel(_repository, SavedChanged),
            _ => new S7ViewModel(_repository, SavedChanged),
        };
    }

    private void WireTab(ConnectionViewModelBase vm)
    {
        vm.CloseRequested += OnTabCloseRequested;
        vm.CloseTabsToLeftRequested += OnCloseTabsToLeftRequested;
        vm.CloseTabsToRightRequested += OnCloseTabsToRightRequested;
        vm.CloseAllTabsRequested += OnCloseAllTabsRequested;
    }

    private void UnwireTab(ConnectionViewModelBase vm)
    {
        vm.CloseRequested -= OnTabCloseRequested;
        vm.CloseTabsToLeftRequested -= OnCloseTabsToLeftRequested;
        vm.CloseTabsToRightRequested -= OnCloseTabsToRightRequested;
        vm.CloseAllTabsRequested -= OnCloseAllTabsRequested;
    }

    private void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is ConnectionViewModelBase vm)
            CloseTabs(new[] { vm });
    }

    private void OnCloseTabsToLeftRequested(object? sender, EventArgs e)
    {
        if (sender is not ConnectionViewModelBase vm)
            return;

        int index = Tabs.IndexOf(vm);
        if (index > 0)
            CloseTabs(Tabs.Take(index).ToArray());
    }

    private void OnCloseTabsToRightRequested(object? sender, EventArgs e)
    {
        if (sender is not ConnectionViewModelBase vm)
            return;

        int index = Tabs.IndexOf(vm);
        if (index >= 0 && index < Tabs.Count - 1)
            CloseTabs(Tabs.Skip(index + 1).ToArray());
    }

    private void OnCloseAllTabsRequested(object? sender, EventArgs e)
        => CloseTabs(Tabs.ToArray());

    private void CloseTabs(IReadOnlyList<ConnectionViewModelBase> closingTabs)
    {
        if (closingTabs.Count == 0)
            return;

        int selectedIndex = SelectedTab is null ? -1 : Tabs.IndexOf(SelectedTab);

        foreach (ConnectionViewModelBase tab in closingTabs.Distinct().ToArray())
        {
            if (!Tabs.Contains(tab))
                continue;

            UnwireTab(tab);
            Tabs.Remove(tab);
            _ = DisposeTabAsync(tab);
        }

        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(selectedIndex, 0, Tabs.Count - 1)];
    }

    private void OpenSaved(SavedConfigItem item)
    {
        if (Enum.TryParse(item.Entity.Protocol, out ProtocolType protocol))
            AddTab(protocol, item.Entity.ParamsJson, item.DisplayName);
    }

    private void RequestDeleteSaved(SavedConfigItem item)
    {
        int index = AllSavedConfigs.IndexOf(item);
        if (index < 0)
            return;

        FinalizePendingDelete();

        AllSavedConfigs.RemoveAt(index);
        _deletingIds.Add(item.Entity.Id);
        _pendingDelete = item;
        _pendingDeleteIndex = index;
        IsUndoVisible = true;
        ApplyHistoryFilter();

        var cts = new CancellationTokenSource();
        _deleteCts = cts;
        _ = DeleteAfterUndoWindowAsync(item, index, cts);
    }

    private async Task DeleteAfterUndoWindowAsync(SavedConfigItem item, int index, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(DeleteUndoSeconds), cts.Token);

            if (!ReferenceEquals(_pendingDelete, item))
                return;

            _pendingDelete = null;
            _pendingDeleteIndex = -1;
            _deleteCts = null;
            IsUndoVisible = false;

            try
            {
                await _repository.DeleteAsync(item.Entity.Id);
            }
            catch
            {
                RestoreHistoryItem(item, index);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _deletingIds.Remove(item.Entity.Id);
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_pendingDelete is null)
            return;

        _deleteCts?.Cancel();
        SavedConfigItem item = _pendingDelete;
        int index = _pendingDeleteIndex;
        _pendingDelete = null;
        _pendingDeleteIndex = -1;
        _deletingIds.Remove(item.Entity.Id);
        IsUndoVisible = false;
        RestoreHistoryItem(item, index);
    }

    private void FinalizePendingDelete()
    {
        SavedConfigItem? pending = _pendingDelete;
        if (pending is null)
            return;

        int index = _pendingDeleteIndex;
        _deleteCts?.Cancel();
        _deleteCts = null;
        _pendingDelete = null;
        _pendingDeleteIndex = -1;
        IsUndoVisible = false;
        _ = DeleteFromRepositoryAsync(pending, index);
    }

    private async Task DeleteFromRepositoryAsync(SavedConfigItem item, int index)
    {
        try
        {
            await _repository.DeleteAsync(item.Entity.Id);
        }
        catch
        {
            RestoreHistoryItem(item, index);
        }
        finally
        {
            _deletingIds.Remove(item.Entity.Id);
        }
    }

    private void RestoreHistoryItem(SavedConfigItem item, int index)
    {
        if (AllSavedConfigs.Contains(item))
            return;

        AllSavedConfigs.Insert(Math.Clamp(index, 0, AllSavedConfigs.Count), item);
        ApplyHistoryFilter();
    }

    private async Task RefreshSavedConfigsAsync()
    {
        try
        {
            List<ConnectionConfigEntity> list = await _repository.ListAllAsync();
            AllSavedConfigs.Clear();
            foreach (ConnectionConfigEntity entity in list)
            {
                if (_deletingIds.Contains(entity.Id))
                    continue;
                AllSavedConfigs.Add(new SavedConfigItem(entity, OpenSaved, RequestDeleteSaved));
            }
            ApplyHistoryFilter();
        }
        catch
        {
        }
    }

    private void ApplyHistoryFilter()
    {
        string search = HistorySearchText.Trim();
        string group = SelectedHistoryGroup;

        SavedConfigs.Clear();
        foreach (SavedConfigItem item in AllSavedConfigs)
        {
            if (!string.Equals(group, "全部", StringComparison.Ordinal)
                && !string.Equals(item.ProtocolGroup, group, StringComparison.Ordinal))
                continue;

            if (search.Length > 0
                && !Contains(item.DisplayName, search)
                && !Contains(item.ProtocolName, search)
                && !Contains(item.EndpointSummary, search))
                continue;

            SavedConfigs.Add(item);
        }

        HasFilteredSavedConfigs = SavedConfigs.Count > 0;
        OnPropertyChanged(nameof(HasSavedConfigs));
        OnPropertyChanged(nameof(HasActiveHistoryFilter));
        OnPropertyChanged(nameof(ShowNoHistory));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(HistoryCountText));
    }

    private static bool Contains(string? source, string search)
        => source is not null && source.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static async Task DisposeTabAsync(ConnectionViewModelBase vm)
    {
        try
        {
            await vm.DisposeAsync();
        }
        catch
        {
        }
    }
}
