using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ClashWpf.Models;

namespace ClashWpf.Services;

/// <summary>mihomo 引擎进程宿主：定位内核、启动/停止、状态广播、日志捕获、流量采样。</summary>
public interface IClashEngine
{
    ClashEngineState State { get; }
    Process? Process { get; }
    string? Version { get; }
    string? LastError { get; }

    string ResolveCorePath();
    bool IsCorePresent { get; }

    /// <summary>最近 N 行引擎日志（供日志页随时取用）</summary>
    ConcurrentTail LogBuffer { get; }

    /// <summary>引擎日志落盘文件路径（engine.log）</summary>
    string LogFilePath { get; }

    event Action? StateChanged;

    /// <summary>启动引擎（若未生成配置则先生成 config.yaml）。调用方需 UI 上下文安全处理。</summary>
    Task<(bool ok, string message)> StartAsync(CancellationToken ct = default);

    Task StopAsync();
    void Restart();

    /// <summary>释放进程句柄并终止（应用退出兜底）</summary>
    void Dispose();
}

public class ClashEngine : IClashEngine, IDisposable
{
    private readonly ISettingsStore _store;
    private readonly ISubscriptionService _subscriptions;
    private readonly IClashApiClient _api;
    private readonly ISystemProxyService? _sysProxy;

    private Process? _proc;
    private CancellationTokenSource? _lifeCts;
    private readonly object _lock = new();
    private Timer? _trafficTimer;
    private StreamWriter? _logWriter;

    public event Action? StateChanged;

    public ClashEngineState State { get; private set; } = ClashEngineState.Stopped;
    public Process? Process => _proc;
    public string? Version { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>最近 500 行引擎日志（供日志页随时取用）</summary>
    public ConcurrentTail LogBuffer { get; } = new(500);

    public ClashEngine(ISettingsStore store, ISubscriptionService subscriptions, IClashApiClient api,
        ISystemProxyService? sysProxy = null)
    {
        _store = store;
        _subscriptions = subscriptions;
        _api = api;
        _sysProxy = sysProxy;
    }

    public bool IsCorePresent => File.Exists(ResolveCorePath());

    public string ResolveCorePath()
    {
        var s = _store.Settings;
        if (!string.IsNullOrWhiteSpace(s.CorePath) && File.Exists(s.CorePath))
            return s.CorePath;

        // 候选目录
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "core", "mihomo.exe"),
            Path.Combine(AppContext.BaseDirectory, "mihomo.exe"),
            Path.Combine(_store.CoreDir, "mihomo.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "core", "mihomo.exe"),
        };
        foreach (var c in candidates)
        {
            try { if (File.Exists(c)) return Path.GetFullPath(c); } catch { }
        }
        return candidates[0];
    }

    public async Task<(bool ok, string message)> StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State is ClashEngineState.Starting or ClashEngineState.Running)
                return (false, "引擎已在运行");
        }

        var core = ResolveCorePath();
        if (!File.Exists(core))
        {
            LastError = $"未找到 mihomo 内核：{core}\n请把 mihomo.exe 放入该目录，或在“设置”页指定内核路径。";
            SetState(ClashEngineState.Failed, LastError);
            return (false, LastError);
        }

        try
        {
            SetState(ClashEngineState.Starting, "正在启动 mihomo…");

            // 1) 确保 config.yaml 存在：先试缓存，无缓存则最简直连配置
            EnsureConfigFile();

            // 2) 启动进程
            var psi = new ProcessStartInfo
            {
                FileName = core,
                WorkingDirectory = _store.RunDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(_store.RunDir);

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += OnOutput;
            _proc.ErrorDataReceived += OnOutput;
            _proc.Exited += (_, _) => OnProcessExited();

            if (!_proc.Start())
            {
                LastError = "进程启动失败。";
                SetState(ClashEngineState.Failed, LastError);
                return (false, LastError);
            }
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            OpenLogFile();

            // 3) 等待 external-controller 就绪
            var ok = await WaitReadyAsync(ct);
            if (!ok)
            {
                await StopAsync();
                LastError = "引擎启动但控制器未在预期时间内就绪。日志：\n" + LogBuffer.Last(15);
                SetState(ClashEngineState.Failed, LastError);
                return (false, LastError);
            }

            var ver = await _api.GetVersionAsync(ct);
            Version = ver?.Version ?? "unknown";
            SetState(ClashEngineState.Running, $"引擎已启动（{Version}），端口 {_store.Settings.MixedPort}");
            StartTrafficSampling();
            return (true, $"启动成功 v{Version}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(ClashEngineState.Failed, ex.Message);
            return (false, ex.Message);
        }
    }

    private void EnsureConfigFile()
    {
        if (!File.Exists(_store.ConfigPath))
        {
            if (File.Exists(_store.RawSubscriptionPath))
                _subscriptions.RegenerateFromCache(_store);
            else
                _subscriptions.GenerateMinimalConfig(_store);
        }
    }

    public async Task StopAsync()
    {
        _lifeCts?.Cancel();
        _trafficTimer?.Dispose();
        _trafficTimer = null;
        CloseLogFile();

        Process? p;
        lock (_lock) { p = _proc; _proc = null; }

        if (p != null && !p.HasExited)
        {
            try
            {
                p.Kill(true);
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch { /* already dead */ }
            finally { p.Dispose(); }
        }

        // 系统代理若仍指向本机端口则关闭（StopAsync 用于手动停止或启动失败清理）
        _sysProxy?.DisableIfPointingTo(_store.Settings.MixedPort);

        SetState(ClashEngineState.Stopped, "引擎已停止");
    }

    public void Restart()
    {
        _ = Task.Run(async () =>
        {
            await StopAsync();
            await StartAsync();
        });
    }

    private void OnProcessExited()
    {
        _trafficTimer?.Dispose();
        _trafficTimer = null;
        CloseLogFile();
        lock (_lock)
        {
            if (_proc != null && _proc.HasExited)
                _proc = null;
        }
        if (State is ClashEngineState.Running or ClashEngineState.Starting)
        {
            // 引擎意外退出：同样清理系统代理，避免浏览器连到已死的端口
            _sysProxy?.DisableIfPointingTo(_store.Settings.MixedPort);
            SetState(ClashEngineState.Failed, "引擎进程意外退出。");
        }
    }

    /// <summary>引擎日志文件（进程启动时开启，停止时关闭）</summary>
    public string LogFilePath => Path.Combine(_store.RunDir, "engine.log");

    private void OpenLogFile()
    {
        try
        {
            Directory.CreateDirectory(_store.RunDir);
            // 简单轮转：超过 5MB 则从空文件重新开始
            var fi = new FileInfo(LogFilePath);
            if (fi.Exists && fi.Length > 5 * 1024 * 1024) fi.Delete();

            _logWriter = new StreamWriter(LogFilePath, append: true, new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true,
            };
        }
        catch { /* 写盘失败不影响引擎运行 */ }
    }

    private void CloseLogFile()
    {
        try { _logWriter?.Dispose(); } catch { }
        _logWriter = null;
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data)) return;
        lock (_lock)
        {
            try { _logWriter?.WriteLine(e.Data); } catch { }
        }
        LogBuffer.Add(e.Data);
        EngineEvents.RaiseLog(this, e.Data, e.Data.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> WaitReadyAsync(CancellationToken ct)
    {
        for (int i = 0; i < 80; i++) // 80 * 250ms = 20s
        {
            if (ct.IsCancellationRequested) return false;
            try
            {
                if (await _api.GetVersionAsync(ct) != null) return true;
            }
            catch { }
            await Task.Delay(250, ct);
        }
        return false;
    }

    private void StartTrafficSampling()
    {
        _lifeCts?.Cancel();
        _lifeCts = new CancellationTokenSource();
        var cts = _lifeCts;

        _trafficTimer = new Timer(async _ =>
        {
            try
            {
                var cur = await _api.GetTrafficAsync(cts.Token);
                EngineEvents.RaiseTraffic(this, new TrafficSampleMessage
                {
                    UpBytes = cur.UpTotal,
                    DownBytes = cur.DownTotal,
                    UpSpeedBps = cur.Up,
                    DownSpeedBps = cur.Down,
                });
            }
            catch { }
        }, null, 0, 1000);
    }

    private void SetState(ClashEngineState state, string detail = "")
    {
        State = state;
        LastError = state == ClashEngineState.Failed ? detail : LastError;
        EngineEvents.RaiseState(this, new EngineStateMessage(state, detail));
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        _lifeCts?.Cancel();
        _lifeCts?.Dispose();
        _trafficTimer?.Dispose();
        CloseLogFile();
        try { _proc?.Kill(true); } catch { }
        _proc?.Dispose();
        _proc = null;
        // 退出程序时兜底清理系统代理
        _sysProxy?.DisableIfPointingTo(_store.Settings.MixedPort);
    }
}

/// <summary>并发安全的尾截断集合</summary>
public class ConcurrentTail
{
    private readonly object _lock = new();
    private readonly Queue<string> _q;
    private readonly int _max;

    public ConcurrentTail(int max)
    {
        _max = max;
        _q = new Queue<string>(max + 1);
    }

    public void Add(string line)
    {
        lock (_lock)
        {
            _q.Enqueue(line);
            while (_q.Count > _max) _q.Dequeue();
        }
    }

    public string Last(int n)
    {
        lock (_lock)
            return string.Join("\n", _q.TakeLast(Math.Min(n, _q.Count)));
    }

    public IReadOnlyList<string> ToList()
    {
        lock (_lock) return _q.ToList();
    }
}

/// <summary>把引擎侧事件统一派发到 UI 线程（通过 Dispatcher）</summary>
public static class EngineEvents
{
    public static Action<object>? PostHandler;

    private static void Post(object msg)
    {
        if (PostHandler != null)
        {
            PostHandler(msg);
            return;
        }
        // 未初始化（非 UI 环境）时静默丢弃
    }

    public static void RaiseState(IClashEngine engine, EngineStateMessage msg) => Post(msg);
    public static void RaiseLog(IClashEngine engine, string line, bool isError) =>
        Post(new LogLineMessage { Line = line, IsError = isError });
    public static void RaiseTraffic(IClashEngine engine, TrafficSampleMessage msg) => Post(msg);
}
