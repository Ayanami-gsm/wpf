using System.Collections.ObjectModel;
using System.Windows.Threading;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>活动连接页：轮询 /connections 展示活动连接，可断开。</summary>
public class ConnectionsViewModel : Screen, IHandle<EngineStateMessage>
{
    private readonly IClashApiClient _api;
    private readonly IEventAggregator _events;
    private readonly DispatcherTimer _timer;
    private bool _polling;
    private bool _busy;

    public ObservableCollection<ConnectionItem> Rows { get; } = new();

    private string _summary = "引擎未运行";
    public string Summary { get => _summary; set { _summary = value; NotifyOfPropertyChange(() => Summary); } }

    public ConnectionsViewModel(IClashApiClient api, IEventAggregator events)
    {
        _api = api;
        _events = events;
        _events.SubscribeOnUIThread(this);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await PollOnceAsync();
    }

    public Task HandleAsync(EngineStateMessage message, CancellationToken cancellationToken)
    {
        if (message.State == ClashEngineState.Running)
            StartPolling();
        else
            StopPolling();
        return Task.CompletedTask;
    }

    public void StartPolling()
    {
        if (_polling) return;
        _polling = true;
        _timer.Start();
        _ = PollOnceAsync();
    }

    public void StopPolling()
    {
        _polling = false;
        _timer.Stop();
        Summary = "引擎未运行";
    }

    private async Task PollOnceAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var snap = await _api.GetConnectionsAsync();
            Rows.Clear();
            foreach (var c in snap.Connections.OrderByDescending(c => c.Upload + c.Download).Take(200))
                Rows.Add(c);
            Summary = $"{Rows.Count} 条连接   ↑ {FormatBytes(snap.UploadTotal)}   ↓ {FormatBytes(snap.DownloadTotal)}";

            // 取当前流量最大的连接，其链路末端即真实出口节点 → 广播给顶部栏
            var top = snap.Connections.OrderByDescending(c => c.Upload + c.Download).FirstOrDefault();
            if (top is { Chains.Count: > 0 })
            {
                var leaf = top.Chains[^1];
                _ = _events.PublishOnCurrentThreadAsync(new ActiveNodeMessage { Node = leaf });
            }
        }
        catch { /* ignore transient */ }
        finally { _busy = false; }
    }

    public async void CloseAll()
    {
        await _api.CloseAllConnectionsAsync();
        await PollOnceAsync();
    }

    public async void CloseSelected(ConnectionItem item)
    {
        if (item == null) return;
        await _api.CloseConnectionAsync(item.Id);
        await PollOnceAsync();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.00} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }
}
