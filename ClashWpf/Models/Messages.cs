using ClashWpf.Models;

namespace ClashWpf.Models;

/// <summary>引擎状态变化消息（经 IEventAggregator 广播）</summary>
public class EngineStateMessage
{
    public ClashEngineState State { get; init; }
    public string Detail { get; init; } = "";

    public EngineStateMessage(ClashEngineState state, string detail = "")
    {
        State = state;
        Detail = detail;
    }
}

/// <summary>每秒一次流量采样</summary>
public class TrafficSampleMessage
{
    public long UpBytes { get; init; }
    public long DownBytes { get; init; }
    public double UpSpeedBps { get; init; }
    public double DownSpeedBps { get; init; }
}

/// <summary>日志行（引擎 stdout 捕获）</summary>
public class LogLineMessage
{
    public string Line { get; init; } = "";
    public bool IsError { get; init; }
}

/// <summary>配置/订阅更新结果</summary>
public class ProfileUpdatedMessage
{
    public bool Success { get; init; }
    public string Detail { get; init; } = "";
}

/// <summary>当前实际出口节点（由连接页每秒轮询得出，广播给顶部栏）</summary>
public class ActiveNodeMessage
{
    public string Node { get; init; } = "—";
}
