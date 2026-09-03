using System.IO;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>仪表盘：引擎启停、系统代理开关、实时速率与流量汇总。</summary>
public class DashboardViewModel : Screen,
    IHandle<EngineStateMessage>,
    IHandle<TrafficSampleMessage>
{
    private readonly IClashEngine _engine;
    private readonly ISettingsStore _store;
    private readonly ISubscriptionService _subs;
    private readonly IClashApiClient _api;
    private readonly ISystemProxyService _sysProxy;
    private readonly IEventAggregator _events;
    private bool _handlingProxyChange;

    public DashboardViewModel(
        IClashEngine engine,
        ISettingsStore store,
        ISubscriptionService subs,
        IClashApiClient api,
        ISystemProxyService sysProxy,
        IEventAggregator events)
    {
        _engine = engine;
        _store = store;
        _subs = subs;
        _api = api;
        _sysProxy = sysProxy;
        _events = events;
        _events.SubscribeOnUIThread(this);
    }

    // ---------- 状态展示 ----------
    private string _stateText = "已停止";
    public string StateText { get => _stateText; set { _stateText = value; NotifyOfPropertyChange(() => StateText); } }

    private string _stateDetail = "";
    public string StateDetail { get => _stateDetail; set { _stateDetail = value; NotifyOfPropertyChange(() => StateDetail); } }

    private string _versionText = "—";
    public string VersionText { get => _versionText; set { _versionText = value; NotifyOfPropertyChange(() => VersionText); } }

    private string _corePath = "";
    public string CorePath { get => _corePath; set { _corePath = value; NotifyOfPropertyChange(() => CorePath); } }

    private string _lastAction = "";
    public string LastAction { get => _lastAction; set { _lastAction = value; NotifyOfPropertyChange(() => LastAction); } }

    // ---------- 实时流量 ----------
    private string _upSpeed = "0 KB/s";
    public string UpSpeed { get => _upSpeed; set { _upSpeed = value; NotifyOfPropertyChange(() => UpSpeed); } }

    private string _downSpeed = "0 KB/s";
    public string DownSpeed { get => _downSpeed; set { _downSpeed = value; NotifyOfPropertyChange(() => DownSpeed); } }

    private string _totalUp = "0";
    public string TotalUp { get => _totalUp; set { _totalUp = value; NotifyOfPropertyChange(() => TotalUp); } }

    private string _totalDown = "0";
    public string TotalDown { get => _totalDown; set { _totalDown = value; NotifyOfPropertyChange(() => TotalDown); } }

    public bool CanStart => _engine.State is not (ClashEngineState.Running or ClashEngineState.Starting);
    public bool CanStop => _engine.State is ClashEngineState.Running or ClashEngineState.Starting;

    private bool _sysProxyChecked;
    public bool SysProxyChecked
    {
        get => _sysProxyChecked;
        set
        {
            if (_handlingProxyChange) return;
            _handlingProxyChange = true;
            try
            {
                _sysProxyChecked = value;
                NotifyOfPropertyChange(() => SysProxyChecked);
                ApplySystemProxy();
            }
            finally { _handlingProxyChange = false; }
        }
    }

    public Task HandleAsync(EngineStateMessage message, CancellationToken cancellationToken)
    {
        UpdateFromEngine();
        return Task.CompletedTask;
    }

    public Task HandleAsync(TrafficSampleMessage m, CancellationToken cancellationToken)
    {
        UpSpeed = FormatSpeed(m.UpSpeedBps);
        DownSpeed = FormatSpeed(m.DownSpeedBps);
        TotalUp = FormatBytes(m.UpBytes);
        TotalDown = FormatBytes(m.DownBytes);
        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        CorePath = _engine.ResolveCorePath();
        UpdateFromEngine();
        var ver = await _api.GetVersionAsync();
        VersionText = ver?.Version ?? "—";
    }

    private void UpdateFromEngine()
    {
        StateText = _engine.State switch
        {
            ClashEngineState.Running => "运行中",
            ClashEngineState.Starting => "启动中…",
            ClashEngineState.Failed => "异常",
            _ => "已停止",
        };
        StateDetail = _engine.LastError ?? "";
        NotifyOfPropertyChange(() => CanStart);
        NotifyOfPropertyChange(() => CanStop);
        if (_engine.State == ClashEngineState.Stopped)
        {
            UpSpeed = DownSpeed = "0 KB/s";
            TotalUp = TotalDown = "0";
            if (_sysProxyChecked)
            {
                // 引擎停止：真正关闭系统代理（若它正指向本机端口），而不是只取消勾选
                _handlingProxyChange = true;
                _sysProxyChecked = false;
                NotifyOfPropertyChange(() => SysProxyChecked);
                _handlingProxyChange = false;
                _sysProxy.DisableIfPointingTo(_store.Settings.MixedPort);
                LastAction = "引擎已停止，系统代理已自动关闭";
            }
        }
        else if (_engine.State == ClashEngineState.Running && !_handlingProxyChange)
        {
            _handlingProxyChange = true;
            _sysProxyChecked = _sysProxy.IsSystemProxyEnabled;
            NotifyOfPropertyChange(() => SysProxyChecked);
            _handlingProxyChange = false;
        }
    }

    // ---------- 动作 ----------
    public async void Start()
    {
        LastAction = "正在准备…";
        try
        {
            // 若从未生成过 config.yaml，先尝试拉取订阅；失败则退化为最简配置
            if (!File.Exists(_store.ConfigPath))
            {
                if (!string.IsNullOrWhiteSpace(_store.Settings.SubscriptionUrl))
                {
                    LastAction = "正在拉取订阅并生成配置…";
                    await _subs.FetchAndGenerateAsync(_store);
                }
                else
                {
                    _subs.GenerateMinimalConfig(_store);
                }
            }

            var (ok, msg) = await _engine.StartAsync();
            LastAction = ok ? "启动成功" : "启动失败";
            if (!ok) StateDetail = msg;
            UpdateFromEngine();
        }
        catch (Exception ex)
        {
            LastAction = "启动失败：" + ex.Message;
            StateDetail = ex.Message;
        }
    }

    public async void Stop()
    {
        try
        {
            await _engine.StopAsync();
            LastAction = "已停止";
        }
        catch (Exception ex)
        {
            LastAction = "停止失败：" + ex.Message;
        }
        UpdateFromEngine();
    }

    public async void RefreshSubscription()
    {
        LastAction = "正在更新订阅…";
        try
        {
            await _subs.FetchAndGenerateAsync(_store);
            LastAction = _engine.State == ClashEngineState.Running
                ? "订阅已更新（需重启引擎生效）"
                : "订阅已更新并生成配置";
            StateDetail = "配置文件已重建：" + _store.ConfigPath;
        }
        catch (Exception ex)
        {
            LastAction = "订阅更新失败：" + ex.Message;
        }
    }

    private void ApplySystemProxy()
    {
        if (_sysProxyChecked)
        {
            if (_engine.State != ClashEngineState.Running)
            {
                LastAction = "请先启动引擎再开启系统代理";
                return;
            }
            _sysProxy.Enable(_store.Settings.MixedPort);
            LastAction = $"系统代理已开启（127.0.0.1:{_store.Settings.MixedPort}）";
        }
        else
        {
            _sysProxy.Disable();
            LastAction = "系统代理已关闭";
        }
    }

    public void OpenRunDir()
    {
        try
        {
            if (Directory.Exists(_store.RunDir))
                System.Diagnostics.Process.Start("explorer.exe", _store.RunDir);
        }
        catch (Exception ex) { LastAction = ex.Message; }
    }

    private static string FormatSpeed(double bps)
    {
        if (bps >= 1024 * 1024) return $"{bps / 1024 / 1024:0.00} MB/s";
        if (bps >= 1024) return $"{bps / 1024:0} KB/s";
        return $"{bps:0} B/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.00} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024:0.00} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }
}
