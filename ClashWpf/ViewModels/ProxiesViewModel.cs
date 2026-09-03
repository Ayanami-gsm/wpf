using System.Collections.ObjectModel;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>代理节点页：列出代理组与成员、测延迟、切换节点。</summary>
public class ProxiesViewModel : Screen, IHandle<EngineStateMessage>
{
    private readonly IClashApiClient _api;
    private readonly ISettingsStore _store;
    private readonly IEventAggregator _events;

    public ObservableCollection<ProxyGroupView> Groups { get; } = new();

    private ProxyGroupView? _selectedGroup;
    public ProxyGroupView? SelectedGroup
    {
        get => _selectedGroup;
        set { _selectedGroup = value; NotifyOfPropertyChange(() => SelectedGroup); }
    }

    private string _status = "";
    public string Status { get => _status; set { _status = value; NotifyOfPropertyChange(() => Status); } }

    public ProxiesViewModel(IClashApiClient api, ISettingsStore store, IEventAggregator events)
    {
        _api = api;
        _store = store;
        _events = events;
        _events.SubscribeOnUIThread(this);
    }

    public Task HandleAsync(EngineStateMessage message, CancellationToken cancellationToken)
    {
        if (message.State == ClashEngineState.Running)
            _ = ReloadAsync();
        else if (message.State is ClashEngineState.Stopped or ClashEngineState.Failed)
        {
            Groups.Clear();
            SelectedGroup = null;
            Status = "引擎未运行";
        }
        return Task.CompletedTask;
    }

    /// <summary>Caliburn.Micro 按钮入口（XAML: [Click]=[Reload]，CM 不自动映射 ReloadAsync）</summary>
    public void Reload() => _ = ReloadAsync();

    public async Task ReloadAsync()
    {
        Status = "加载中…";
        try
        {
            var entries = await _api.GetProxiesAsync();
            var groups = entries.Where(e => e.IsGroup).ToList();

            var views = new List<ProxyGroupView>();
            foreach (var g in groups)
            {
                var view = new ProxyGroupView
                {
                    Name = g.Name,
                    Type = g.Type,
                    Now = g.Now,
                };
                foreach (var m in g.All)
                {
                    view.Nodes.Add(new ProxyNodeView
                    {
                        Name = m,
                        IsCurrent = string.Equals(g.Now, m, StringComparison.Ordinal),
                    });
                }
                views.Add(view);
            }

            Groups.Clear();
            foreach (var v in views) Groups.Add(v);
            Status = $"{Groups.Count} 个代理组；{entries.Count(e => !e.IsGroup)} 个节点。";
        }
        catch (Exception ex)
        {
            Status = "加载失败：" + ex.Message;
        }
    }

    public async void TestSelectedGroupDelay()
    {
        if (SelectedGroup == null) { Status = "请先选择一个代理组。"; return; }
        await TestGroupNodesAsync(SelectedGroup);
    }

    public async void TestAllGroups()
    {
        Status = "正在对全部代理组测延迟…";
        foreach (var g in Groups.ToList())
            await TestGroupNodesAsync(g);
        Status = "全部组延迟测试完成。";
    }

    /// <summary>对组内每个节点并行测延迟并就地刷新延迟列</summary>
    private async Task TestGroupNodesAsync(ProxyGroupView g)
    {
        var s = _store.Settings;
        var nodes = g.Nodes.Where(n => n.Name != "DIRECT" && n.Name != "REJECT").ToList();
        if (nodes.Count == 0) return;

        Status = $"正在测 {g.Name}（{nodes.Count} 个节点）…";

        using var sem = new SemaphoreSlim(8); // 并发 8，避免打爆
        var tasks = nodes.Select(async n =>
        {
            await sem.WaitAsync();
            try
            {
                n.DelayMs = await _api.TestDelayAsync(n.Name, s.LatencyTestUrl, s.LatencyTestTimeoutMs);
            }
            catch { n.DelayMs = null; }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        Status = $"{g.Name} 测延迟完成：{nodes.Count(n => n.DelayMs.HasValue)} 可用";
    }

    public async void SelectNode(ProxyNodeView node)
    {
        if (SelectedGroup == null || node == null) return;
        try
        {
            var ok = await _api.SelectProxyAsync(SelectedGroup.Name, node.Name);
            Status = ok
                ? $"已切换到 {SelectedGroup.Name} → {node.Name}"
                : "切换失败（组内可能不含该节点，或节点已下线）";
            if (ok) await ReloadAsync();
        }
        catch (Exception ex)
        {
            Status = "切换出错：" + ex.Message;
        }
    }
}
