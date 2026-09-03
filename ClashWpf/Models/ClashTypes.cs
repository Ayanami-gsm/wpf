using System.Collections.ObjectModel;
using Caliburn.Micro;

namespace ClashWpf.Models;

public enum ClashEngineState
{
    Stopped,
    Starting,
    Running,
    Failed,
}

/// <summary>REST /version 返回（如 {"meta":true,"version":"v1.19.30"}）</summary>
public class VersionInfo
{
    public string? Version { get; set; }
    public bool Meta { get; set; }
}

/// <summary>GET /proxies 中的一个条目（代理节点或代理组）</summary>
public class ProxyEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Now { get; set; }
    public List<string> All { get; set; } = new();
    public List<string> History { get; set; } = new();
    public bool Alive { get; set; }
    public bool IsGroup =>
        Type is "Selector" or "URLTest" or "Fallback" or "LoadBalance" or "Relay";
}

/// <summary>代理组 + 成员列表（UI 用）</summary>
public class ProxyGroupView : PropertyChangedBase
{
    private string _name = "";
    public string Name { get => _name; set { _name = value; NotifyOfPropertyChange(() => Name); } }

    private string _type = "";
    public string Type { get => _type; set { _type = value; NotifyOfPropertyChange(() => Type); } }

    private string? _now;
    public string? Now { get => _now; set { _now = value; NotifyOfPropertyChange(() => Now); } }

    public ObservableCollection<ProxyNodeView> Nodes { get; } = new();
}

public class ProxyNodeView : PropertyChangedBase
{
    private string _name = "";
    public string Name { get => _name; set { _name = value; NotifyOfPropertyChange(() => Name); } }

    private int? _delayMs;
    public int? DelayMs
    {
        get => _delayMs;
        set { _delayMs = value; NotifyOfPropertyChange(() => DelayMs); NotifyOfPropertyChange(() => DelayText); }
    }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; NotifyOfPropertyChange(() => IsCurrent); }
    }

    public string DelayText => DelayMs.HasValue ? $"{DelayMs.Value} ms" : "—";
}

/// <summary>一条活动连接</summary>
public class ConnectionItem
{
    public string Id { get; set; } = "";
    public string Host { get; set; } = "";
    public string Network { get; set; } = "";      // tcp / udp
    public string Rule { get; set; } = "";
    public List<string> Chains { get; set; } = new();
    public long Upload { get; set; }
    public long Download { get; set; }
    public string Start { get; set; } = "";

    public string ChainsText => string.Join(" → ", Chains);
    public string HostPort => Host;
}

public class ConnectionsSnapshot
{
    public long DownloadTotal { get; set; }
    public long UploadTotal { get; set; }
    public List<ConnectionItem> Connections { get; set; } = new();
}

/// <summary>GET /traffic 返回（自进程启动累计字节）</summary>
public class TrafficPoint
{
    public long Up { get; set; }
    public long Down { get; set; }
    public long UpTotal { get; set; }
    public long DownTotal { get; set; }
}
