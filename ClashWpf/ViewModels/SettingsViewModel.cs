using System.IO;
using System.Windows;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>设置页：订阅地址、内核路径、端口、模式等。</summary>
public class SettingsViewModel : Screen
{
    private readonly ISettingsStore _store;
    private readonly ISubscriptionService _subs;

    public AppSettings S => _store.Settings;

    private string _message = "";
    public string Message { get => _message; set { _message = value; NotifyOfPropertyChange(() => Message); } }

    public string SubscriptionUrl { get => _store.Settings.SubscriptionUrl; set { _store.Settings.SubscriptionUrl = value; NotifyOfPropertyChange(() => SubscriptionUrl); } }
    public string CorePath { get => _store.Settings.CorePath; set { _store.Settings.CorePath = value; NotifyOfPropertyChange(() => CorePath); } }
    public string MixedPort { get => _store.Settings.MixedPort.ToString(); set { if (int.TryParse(value, out var v)) _store.Settings.MixedPort = v; NotifyOfPropertyChange(() => MixedPort); } }
    public string ControllerPort { get => _store.Settings.ControllerPort.ToString(); set { if (int.TryParse(value, out var v)) _store.Settings.ControllerPort = v; NotifyOfPropertyChange(() => ControllerPort); } }
    public string Secret { get => _store.Settings.Secret; set { _store.Settings.Secret = value; NotifyOfPropertyChange(() => Secret); } }
    public bool AllowLan { get => _store.Settings.AllowLan; set { _store.Settings.AllowLan = value; NotifyOfPropertyChange(() => AllowLan); } }
    public string LatencyTestUrl { get => _store.Settings.LatencyTestUrl; set { _store.Settings.LatencyTestUrl = value; NotifyOfPropertyChange(() => LatencyTestUrl); } }

    public string[] Modes { get; } = { "rule", "global", "direct" };

    public string SelectedMode
    {
        get => _store.Settings.Mode;
        set { _store.Settings.Mode = value; NotifyOfPropertyChange(() => SelectedMode); }
    }

    public SettingsViewModel(ISettingsStore store, ISubscriptionService subs)
    {
        _store = store;
        _subs = subs;
    }

    public void Save()
    {
        try
        {
            _store.Save();
            Message = $"已保存到 {_store.SettingsPath}";
        }
        catch (Exception ex)
        {
            Message = "保存失败：" + ex.Message;
        }
    }

    public void BrowseCore()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 mihomo.exe",
            Filter = "mihomo 内核|mihomo.exe|可执行文件|*.exe|所有文件|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() == true)
        {
            CorePath = dlg.FileName;
            _store.Save();
            Message = $"内核已设为 {dlg.FileName}";
        }
    }

    public void TestSubscription()
    {
        Message = "正在测试订阅…";
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await _subs.FetchAndGenerateAsync(_store);
                var (p, g, r) = _subs.CountProfile(text);
                SafeSetMessage($"订阅解析成功：{p} 个代理节点、{g} 个代理组、{r} 条规则。config.yaml 已生成。");
            }
            catch (Exception ex)
            {
                SafeSetMessage("订阅失败：" + ex.Message);
            }
        });
    }

    public void OpenConfigDir()
    {
        try
        {
            if (Directory.Exists(_store.RunDir))
                System.Diagnostics.Process.Start("explorer.exe", _store.RunDir);
        }
        catch (Exception ex) { Message = ex.Message; }
    }

    /// <summary>PasswordBox 无法绑定，改用事件消息回写</summary>
    public void OnSecretChanged(string password) => Secret = password;

    private void SafeSetMessage(string msg)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        if (dispatcher.CheckAccess()) Message = msg;
        else dispatcher.BeginInvoke(() => Message = msg);
    }
}
