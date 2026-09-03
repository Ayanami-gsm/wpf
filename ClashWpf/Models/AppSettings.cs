namespace ClashWpf.Models;

/// <summary>用户可配置项，持久化到 %APPDATA%\ClashWpf\settings.json</summary>
public class AppSettings
{
    public string SubscriptionUrl { get; set; } = "";

    /// <summary>mihomo 可执行文件路径（留空则自动搜索候选目录）</summary>
    public string CorePath { get; set; } = "";

    public int MixedPort { get; set; } = 7890;

    /// <summary>external-controller 端口（REST API）</summary>
    public int ControllerPort { get; set; } = 9090;

    public string ControllerHost { get; set; } = "127.0.0.1";

    public string Secret { get; set; } = "";

    public bool AllowLan { get; set; } = false;

    public string Mode { get; set; } = "rule"; // rule | global | direct

    public string LogLevel { get; set; } = "info";

    /// <summary>订阅更新用 User-Agent（部分机场按 UA 白名单返回格式）</summary>
    public string SubscriptionUserAgent { get; set; } = "clash-verge/v2.0.0";

    /// <summary>延迟测试用 URL</summary>
    public string LatencyTestUrl { get; set; } = "http://www.gstatic.com/generate_204";

    public int LatencyTestTimeoutMs { get; set; } = 5000;
}
