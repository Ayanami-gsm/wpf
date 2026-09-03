using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClashWpf.Services;

/// <summary>切换 Windows 系统代理（WinINET），指向本地 mixed 端口。</summary>
public interface ISystemProxyService
{
    bool IsSystemProxyEnabled { get; }
    /// <summary>当前系统代理是否恰好指向本机某端口（判断是否我们开启的）</summary>
    bool IsPointingTo(int port);
    void Enable(int port);
    void Disable();
    /// <summary>仅当代理指向 port 时才关闭（引擎停止时的安全清理，避免误伤用户自有代理）</summary>
    void DisableIfPointingTo(int port);
    void RefreshWinInet();
}

public class SystemProxyService : ISystemProxyService
{
    private const string InternetSettingsKey =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    // 开启前保存的旧值，Disable 时还原（避免覆盖用户原本的代理配置）
    private int? _priorEnable;
    private string? _priorServer;
    private string? _priorOverride;

    public bool IsSystemProxyEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey);
            return key?.GetValue("ProxyEnable") is int v && v == 1;
        }
    }

    public bool IsPointingTo(int port)
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey);
        if (key?.GetValue("ProxyEnable") is not int v || v != 1) return false;
        var server = key.GetValue("ProxyServer")?.ToString() ?? "";
        var target = $"127.0.0.1:{port}";
        return server.Equals(target, StringComparison.OrdinalIgnoreCase)
               || server.Equals($"localhost:{port}", StringComparison.OrdinalIgnoreCase);
    }

    public void Enable(int port)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKey);
        // 记录旧值用于还原
        if (!_priorEnable.HasValue)
        {
            _priorEnable = key.GetValue("ProxyEnable") as int?;
            _priorServer = key.GetValue("ProxyServer")?.ToString();
            _priorOverride = key.GetValue("ProxyOverride")?.ToString();
        }
        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", $"127.0.0.1:{port}", RegistryValueKind.String);
        key.SetValue("ProxyOverride", "<local>", RegistryValueKind.String);
        RefreshWinInet();
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKey);
        if (_priorEnable.HasValue && _priorEnable.Value == 1)
        {
            // 用户原本就开着别的代理 → 还原它
            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            if (_priorServer != null) key.SetValue("ProxyServer", _priorServer, RegistryValueKind.String);
            if (_priorOverride != null) key.SetValue("ProxyOverride", _priorOverride, RegistryValueKind.String);
        }
        else
        {
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        }
        _priorEnable = null; _priorServer = null; _priorOverride = null;
        RefreshWinInet();
    }

    public void DisableIfPointingTo(int port)
    {
        if (IsPointingTo(port)) Disable();
    }

    public void RefreshWinInet()
    {
        try
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("WinINet 刷新失败: " + ex.Message);
        }
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(
        IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
