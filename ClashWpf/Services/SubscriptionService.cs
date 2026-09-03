using System.IO;
using System.Net.Http;
using System.Text;
using ClashWpf.Models;

namespace ClashWpf.Services;

/// <summary>
/// 订阅拉取与 config.yaml 生成。
/// 生成策略：下载订阅 YAML → 作为基础，把本应用的强制项（端口/控制器/安全项）覆盖进去 → 写 run\config.yaml。
/// </summary>
public interface ISubscriptionService
{
    /// <summary>下载订阅并生成合并后的 config.yaml；同时缓存原始订阅</summary>
    Task<string> FetchAndGenerateAsync(ISettingsStore store, CancellationToken ct = default);

    /// <summary>仅基于缓存的原始订阅重新生成（离线可用）</summary>
    string RegenerateFromCache(ISettingsStore store);

    /// <summary>生成最简直连配置（无订阅时兜底）</summary>
    string GenerateMinimalConfig(ISettingsStore store);

    /// <summary>解析出合并结果摘要（用于 UI 展示代理数量等）</summary>
    (int proxies, int groups, int rules) CountProfile(string yamlText);
}

public class SubscriptionService : ISubscriptionService
{
    private static readonly HttpClient SharedHttp = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                     | System.Net.DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
        };
        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public async Task<string> FetchAndGenerateAsync(ISettingsStore store, CancellationToken ct = default)
    {
        var s = store.Settings;
        if (string.IsNullOrWhiteSpace(s.SubscriptionUrl))
            throw new InvalidOperationException("尚未配置订阅地址（Settings 页填写 SubscriptionUrl）。");

        using var req = new HttpRequestMessage(HttpMethod.Get, s.SubscriptionUrl.Trim());
        req.Headers.TryAddWithoutValidation("User-Agent", s.SubscriptionUserAgent);
        req.Headers.TryAddWithoutValidation("Accept",
            "application/octet-stream, application/yaml, text/yaml, */*;q=0.5");

        using var resp = await SharedHttp.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"订阅下载失败 HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var text = DecodeBody(bytes);

        // 缓存原始订阅，便于下次离线重建
        Directory.CreateDirectory(store.RunDir);
        await File.WriteAllTextAsync(store.RawSubscriptionPath, text, Encoding.UTF8, ct);

        WriteMergedConfig(store, text);
        return text;
    }

    public string RegenerateFromCache(ISettingsStore store)
    {
        if (!File.Exists(store.RawSubscriptionPath))
            return GenerateMinimalConfig(store);
        var text = File.ReadAllText(store.RawSubscriptionPath);
        WriteMergedConfig(store, text);
        return text;
    }

    /// <summary>有些机场把 YAML 内容做了 gzip(base64)。此处尽量智能解码。</summary>
    private static string DecodeBody(byte[] bytes)
    {
        // 先尝试直接按 UTF-8 读取
        var direct = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').TrimStart();
        if (direct.Contains("proxies:") || direct.Contains("proxy-groups:"))
            return direct;

        // 尝试 base64（纯 ASCII 且含 base64 特征）
        var text = Encoding.UTF8.GetString(bytes);
        var candidate = text.Trim().Replace("\r", "").Replace("\n", "");
        if (candidate.Length > 64 && IsLikelyBase64(candidate))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(candidate));
                if (decoded.Contains("proxies:") || decoded.Contains("proxy-groups:"))
                    return decoded;
            }
            catch (FormatException)
            {
                // ignore
            }
        }
        return direct;
    }

    private static bool IsLikelyBase64(string s)
    {
        foreach (var c in s)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '='))
                return false;
        }
        return s.Length % 4 == 0;
    }

    /// <summary>YAML 文本级合并：在订阅末尾追加覆盖段。<br/>
    /// 说明：Clash/Mihomo 的 YAML 由 yaml.v3 解析，重复的顶层键会报错，因此这里用
    /// “删除原键后写入新键”的文本替换策略，而不是简单拼接。</summary>
    private void WriteMergedConfig(ISettingsStore store, string yamlText)
    {
        var cfg = TextMerge.Apply(yamlText, store.Settings);
        Directory.CreateDirectory(store.RunDir);
        File.WriteAllText(store.ConfigPath, cfg, new UTF8Encoding(false));
    }

    public string GenerateMinimalConfig(ISettingsStore store)
    {
        var s = store.Settings;
        var cfg = $$"""
        mixed-port: {{s.MixedPort}}
        allow-lan: {{s.AllowLan.ToString().ToLower()}}
        mode: {{s.Mode}}
        log-level: {{s.LogLevel}}
        ipv6: false
        external-controller: {{s.ControllerHost}}:{{s.ControllerPort}}
        {{(string.IsNullOrEmpty(s.Secret) ? "" : $"secret: {s.Secret}\n")}}
        proxies: []
        proxy-groups: []
        rules:
          - MATCH,DIRECT
        """;
        Directory.CreateDirectory(store.RunDir);
        File.WriteAllText(store.ConfigPath, cfg, new UTF8Encoding(false));
        return cfg;
    }

    public (int proxies, int groups, int rules) CountProfile(string yamlText)
    {
        int proxies = CountTopList(yamlText, "proxies:");
        int groups = CountTopList(yamlText, "proxy-groups:");
        int rules = CountTopList(yamlText, "rules:");
        return (proxies, groups, rules);
    }

    /// <summary>统计顶层列表 key 下的条目数（按行扫描）<br/>
    /// proxies/rules 段："- xxx" 每行一个条目（含单行 JSON 流式条目）。<br/>
    /// proxy-groups 段：条目以 "-" 开头且下一行是 "name:"（避免把组内嵌套节点算进去）。</summary>
    private static int CountTopList(string yaml, string key)
    {
        var lines = yaml.Split('\n');
        bool inSection = false;
        int count = 0;
        bool groupMode = key.StartsWith("proxy-groups", StringComparison.Ordinal);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (!inSection)
            {
                // 行首无缩进且以 key 开头（proxies: / proxy-groups: / rules:）
                if (line.Length > 0 && !char.IsWhiteSpace(line[0])
                    && trimmed.StartsWith(key, StringComparison.Ordinal))
                    inSection = true;
                continue;
            }

            if (line.Length == 0) continue;

            // 遇到新的顶层非注释、非列表项键 → 段结束
            if (!char.IsWhiteSpace(line[0]))
            {
                if (line[0] == '#') continue;
                if (trimmed.StartsWith("-") == false) break;
                // 顶格列表项继续往下走
            }

            bool isItem;
            if (groupMode)
            {
                // 组条目：本行是 "-"，下一行是 name:xxx
                isItem = trimmed == "-" && i + 1 < lines.Length
                         && lines[i + 1].TrimStart().StartsWith("name:", StringComparison.Ordinal);
            }
            else
            {
                isItem = trimmed.StartsWith("- ") || trimmed == "-";
            }

            if (isItem) count++;
        }
        return count;
    }
}

/// <summary>文本级 YAML 覆盖（保留锚点/注释，只重写指定顶层键）</summary>
public static class TextMerge
{
    /// <summary>强制本应用统一的键（写进生成的 config.yaml，优先级高于订阅）<br/>
    /// 注意：secret 一律覆盖为设置值（空串=引擎不鉴权），避免订阅自带 secret 导致控制器握手失败。</summary>
    public static string Apply(string yaml, AppSettings s)
    {
        var overrides = new List<(string key, string value)>
        {
            ("mixed-port", s.MixedPort.ToString()),
            ("allow-lan", s.AllowLan.ToString().ToLower()),
            ("mode", s.Mode),
            ("log-level", s.LogLevel),
            ("external-controller", $"{s.ControllerHost}:{s.ControllerPort}"),
            ("secret", s.Secret.Trim()),
        };
        var text = yaml.TrimEnd() + "\n";
        foreach (var (key, value) in overrides)
            text = UpsertTopLevelKey(text, key, value);
        return text;
    }

    /// <summary>把顶层键 key 设为 value：已有则行替换，没有则在文件末尾追加。<br/>
    /// 兼容裸键与引号键（"key": ...），避免 yaml.v3 重复键报错。</summary>
    private static string UpsertTopLevelKey(string yaml, string key, string value)
    {
        var lines = yaml.Split('\n').ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0])) continue;

            var trimmed = line.TrimStart();
            // 匹配 "key": 或 key:
            if (trimmed.StartsWith("\"" + key + "\":", StringComparison.Ordinal)
                || trimmed.StartsWith(key + ":", StringComparison.Ordinal))
            {
                lines[i] = $"{key}: {value}";
                return string.Join("\n", lines);
            }
        }

        // 未找到：追加到末尾（用空行隔开，避免粘连上一段列表项）
        lines.Add($"{key}: {value}");
        return string.Join("\n", lines);
    }
}
