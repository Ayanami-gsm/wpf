using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClashWpf.Models;
namespace ClashWpf.Services;

/// <summary>访问 mihomo external-controller REST API</summary>
public interface IClashApiClient
{
    Task<VersionInfo?> GetVersionAsync(CancellationToken ct = default);
    Task<List<ProxyEntry>> GetProxiesAsync(CancellationToken ct = default);
    Task<bool> SelectProxyAsync(string groupName, string nodeName, CancellationToken ct = default);
    Task<int?> TestDelayAsync(string proxyName, string url, int timeoutMs, CancellationToken ct = default);
    Task<ConnectionsSnapshot> GetConnectionsAsync(CancellationToken ct = default);
    Task CloseConnectionAsync(string id, CancellationToken ct = default);
    Task CloseAllConnectionsAsync(CancellationToken ct = default);
    Task<TrafficPoint> GetTrafficAsync(CancellationToken ct = default);
    Task<bool> GetIsRunningAsync(CancellationToken ct = default);
}

public class ClashApiClient : IClashApiClient
{
    private readonly ISettingsStore _store;
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                     | System.Net.DecompressionMethods.Deflate,
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
    }

    public ClashApiClient(ISettingsStore store) => _store = store;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private string Base => $"http://{_store.Settings.ControllerHost}:{_store.Settings.ControllerPort}";

    private void ApplyAuth(HttpRequestMessage req)
    {
        var secret = _store.Settings.Secret.Trim();
        if (secret.Length > 0)
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
        string? jsonBody = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(method, Base + path);
        ApplyAuth(req);
        if (jsonBody != null)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await Http.SendAsync(req, ct);
    }

    public async Task<VersionInfo?> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, "/version", null, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<VersionInfo>(json, JsonOpts);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    public async Task<List<ProxyEntry>> GetProxiesAsync(CancellationToken ct = default)
    {
        var list = new List<ProxyEntry>();
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, "/proxies", null, ct);
            if (!resp.IsSuccessStatusCode) return list;
            var json = await resp.Content.ReadAsStringAsync(ct);
            var root = JsonNode.Parse(json);
            if (root?["proxies"] is not JsonObject obj) return list;

            foreach (var (name, node) in obj)
            {
                if (node is not JsonObject n) continue;
                var entry = new ProxyEntry { Name = name ?? "" };
                entry.Type = n["type"]?.GetValue<string>() ?? "";
                entry.Now = n["now"]?.GetValue<string>();
                if (n["alive"] is JsonValue av) entry.Alive = av.GetValue<bool>();
                if (n["all"] is JsonArray allArr)
                    foreach (var x in allArr) entry.All.Add(x?.GetValue<string>() ?? "");
                if (n["history"] is JsonArray hist)
                    foreach (var x in hist)
                    {
                        if (x is JsonObject h && h["delay"] is JsonValue dv)
                        {
                            try { entry.History.Add(dv.GetValue<long>().ToString()); }
                            catch { entry.History.Add(dv.ToJsonString()); }
                        }
                    }
                list.Add(entry);
            }
        }
        catch (HttpRequestException) { /* engine 未就绪 */ }
        return list;
    }

    public async Task<bool> SelectProxyAsync(string groupName, string nodeName, CancellationToken ct = default)
    {
        try
        {
            var path = "/proxies/" + Uri.EscapeDataString(groupName);
            using var resp = await SendAsync(HttpMethod.Put, path, $"{{\"name\":{JsonSerializer.Serialize(nodeName)}}}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
    }

    public async Task<int?> TestDelayAsync(string proxyName, string url, int timeoutMs, CancellationToken ct = default)
    {
        try
        {
            var path = $"/proxies/{Uri.EscapeDataString(proxyName)}/delay"
                       + $"?url={Uri.EscapeDataString(url)}&timeout={timeoutMs}";
            using var resp = await SendAsync(HttpMethod.Get, path, null, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            if (node?["delay"] is JsonValue v)
            {
                try { return v.GetValue<int>(); }
                catch { /* delay 可能为 null 表示失败 */ }
            }
            return null;
        }
        catch (HttpRequestException) { return null; }
    }

    public async Task<ConnectionsSnapshot> GetConnectionsAsync(CancellationToken ct = default)
    {
        var snap = new ConnectionsSnapshot();
        try
        {
            using var resp = await SendAsync(HttpMethod.Get, "/connections", null, ct);
            if (!resp.IsSuccessStatusCode) return snap;
            var json = await resp.Content.ReadAsStringAsync(ct);
            var root = JsonNode.Parse(json);
            if (root is null) return snap;
            snap.DownloadTotal = root["downloadTotal"]?.GetValue<long>() ?? 0;
            snap.UploadTotal = root["uploadTotal"]?.GetValue<long>() ?? 0;
            if (root["connections"] is JsonArray arr)
            {
                foreach (var x in arr)
                {
                    if (x is not JsonObject c) continue;
                    var item = new ConnectionItem
                    {
                        Id = c["id"]?.GetValue<string>() ?? "",
                        Host = c["metadata"]?["host"]?.GetValue<string>()
                               ?? (c["metadata"]?["destinationIP"]?.GetValue<string>() ?? ""),
                        Network = c["metadata"]?["network"]?.GetValue<string>() ?? "",
                        Rule = c["rule"]?.GetValue<string>() ?? "",
                        Upload = c["upload"]?.GetValue<long>() ?? 0,
                        Download = c["download"]?.GetValue<long>() ?? 0,
                        Start = c["start"]?.GetValue<string>() ?? "",
                    };
                    var chains = c["chains"];
                    if (chains is JsonArray ch)
                        foreach (var cc in ch) item.Chains.Add(cc?.GetValue<string>() ?? "");
                    snap.Connections.Add(item);
                }
            }
        }
        catch (HttpRequestException) { /* engine 停止 */ }
        return snap;
    }

    public async Task CloseConnectionAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Delete, "/connections/" + Uri.EscapeDataString(id), null, ct);
        }
        catch (HttpRequestException) { }
    }

    public async Task CloseAllConnectionsAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Delete, "/connections", null, ct);
        }
        catch (HttpRequestException) { }
    }

    /// <summary>
    /// mihomo 的 /traffic 是 chunked 流式接口：每秒推送一帧 JSON，连接永不主动关闭。
    /// 因此不能用默认的 ResponseContentRead（会挂到超时），必须 ResponseHeadersRead + 读完第一帧即断开。
    /// </summary>
    public async Task<TrafficPoint> GetTrafficAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Base + "/traffic");
            ApplyAuth(req);

            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!resp.IsSuccessStatusCode) return new TrafficPoint();

            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // 逐行读：Chunked 传输在 HttpClient 层已解码，每行即一帧 JSON
            while (!cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var node = JsonNode.Parse(line);
                    if (node is null) continue;
                    var p = new TrafficPoint
                    {
                        Up = node["up"]?.GetValue<long>() ?? 0,
                        Down = node["down"]?.GetValue<long>() ?? 0,
                        UpTotal = node["upTotal"]?.GetValue<long>() ?? node["up"]?.GetValue<long>() ?? 0,
                        DownTotal = node["downTotal"]?.GetValue<long>() ?? node["down"]?.GetValue<long>() ?? 0,
                    };
                    return p;
                }
                catch (JsonException)
                {
                    // 非 JSON 行（如空行）继续
                }
            }
            return new TrafficPoint();
        }
        catch (Exception)
        {
            return new TrafficPoint();
        }
    }

    public async Task<bool> GetIsRunningAsync(CancellationToken ct = default)
    {
        return (await GetVersionAsync(ct)) != null;
    }
}
