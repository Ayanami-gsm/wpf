using System.IO;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>外壳：左侧导航 + 内容区（Caliburn Conductor 一屏一活动），顶部栏显示实时节点与速率。</summary>
public class ShellViewModel : Conductor<Screen>.Collection.OneActive,
    IHandle<EngineStateMessage>, IHandle<TrafficSampleMessage>, IHandle<ActiveNodeMessage>
{
    private readonly IClashEngine _engine;
    private readonly ISettingsStore _store;
    private readonly IEventAggregator _events;

    public DashboardViewModel Dashboard { get; }
    public ProxiesViewModel Proxies { get; }
    public ConnectionsViewModel Connections { get; }
    public LogsViewModel Logs { get; }
    public SettingsViewModel Settings { get; }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; NotifyOfPropertyChange(() => StatusText); }
    }

    private string _nodeText = "节点 —";
    public string NodeText
    {
        get => _nodeText;
        set { _nodeText = value; NotifyOfPropertyChange(() => NodeText); }
    }

    private string _speedText = "↑ 0 B/s  ↓ 0 B/s";
    public string SpeedText
    {
        get => _speedText;
        set { _speedText = value; NotifyOfPropertyChange(() => SpeedText); }
    }

    public ShellViewModel(
        IClashEngine engine,
        ISettingsStore store,
        IEventAggregator events,
        DashboardViewModel dashboard,
        ProxiesViewModel proxies,
        ConnectionsViewModel connections,
        LogsViewModel logs,
        SettingsViewModel settings)
    {
        _engine = engine;
        _store = store;
        _events = events;

        Dashboard = dashboard; Dashboard.DisplayName = "仪表盘";
        Proxies = proxies;     Proxies.DisplayName = "代理节点";
        Connections = connections; Connections.DisplayName = "活动连接";
        Logs = logs;           Logs.DisplayName = "日志";
        Settings = settings;   Settings.DisplayName = "设置";

        Items.Add(Dashboard);
        Items.Add(Proxies);
        Items.Add(Connections);
        Items.Add(Logs);
        Items.Add(Settings);
        _events.SubscribeOnUIThread(this);

        SyncStatus(_engine.State, "");
        if (!_engine.IsCorePresent)
            StatusText = "⚠ 未找到 mihomo 内核。请在“设置”页指定内核路径，或将 mihomo.exe 放入 "
                         + Path.Combine(AppContext.BaseDirectory, "core");
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (ActiveItem == null)
            await ActivateItemAsync(Dashboard, cancellationToken);
        _ = Dashboard.RefreshAsync();
    }

    public void ShowDashboard() => _ = ActivateItemAsync(Dashboard);
    public void ShowProxies()
    {
        _ = ActivateItemAsync(Proxies);
        _ = Proxies.ReloadAsync();
    }
    public void ShowConnections()
    {
        _ = ActivateItemAsync(Connections);
        Connections.StartPolling();
    }
    public void ShowLogs() => _ = ActivateItemAsync(Logs);
    public void ShowSettings() => _ = ActivateItemAsync(Settings);

    public Task HandleAsync(EngineStateMessage message, CancellationToken cancellationToken)
    {
        SyncStatus(message.State, message.Detail);
        if (message.State is ClashEngineState.Stopped or ClashEngineState.Failed)
        {
            NodeText = "节点 —";
            SpeedText = "↑ 0 B/s  ↓ 0 B/s";
        }
        return Task.CompletedTask;
    }

    public Task HandleAsync(TrafficSampleMessage m, CancellationToken cancellationToken)
    {
        SpeedText = $"↑ {FormatSpeed(m.UpSpeedBps)}  ↓ {FormatSpeed(m.DownSpeedBps)}";
        return Task.CompletedTask;
    }

    public Task HandleAsync(ActiveNodeMessage m, CancellationToken cancellationToken)
    {
        NodeText = $"节点 {m.Node}";
        return Task.CompletedTask;
    }

    private void SyncStatus(ClashEngineState state, string detail)
    {
        switch (state)
        {
            case ClashEngineState.Running:
                StatusText = $"● 运行中  {_engine.Version}  端口 {_store.Settings.MixedPort}  控制器 {_store.Settings.ControllerHost}:{_store.Settings.ControllerPort}";
                break;
            case ClashEngineState.Starting:
                StatusText = "… 引擎启动中";
                break;
            case ClashEngineState.Failed:
                StatusText = "✕ 引擎异常：" + detail;
                break;
            default:
                StatusText = "○ 引擎已停止";
                break;
        }
    }

    private static string FormatSpeed(double bps)
    {
        if (bps >= 1024 * 1024) return $"{bps / 1024 / 1024:0.00} MB/s";
        if (bps >= 1024) return $"{bps / 1024:0.0} KB/s";
        return $"{bps:0} B/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024:0.00} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }
}
