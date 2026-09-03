using System.IO;
using System.Text.Json;
using ClashWpf.Models;

namespace ClashWpf.Services;

/// <summary>应用目录约定与设置持久化</summary>
public interface ISettingsStore
{
    AppSettings Settings { get; }

    /// <summary>%APPDATA%\ClashWpf</summary>
    string AppDataDir { get; }

    /// <summary>引擎运行时目录（config.yaml 所在目录）</summary>
    string RunDir { get; }

    /// <summary>内核候选目录 core\mihomo.exe</summary>
    string CoreDir { get; }

    string SettingsPath { get; }
    string ConfigPath { get; }
    string RawSubscriptionPath { get; }

    void Load();
    void Save();
}

public class SettingsStore : ISettingsStore
{
    public AppSettings Settings { get; private set; } = new();

    public string AppDataDir { get; }
    public string RunDir { get; }
    public string CoreDir { get; }
    public string SettingsPath { get; }
    public string ConfigPath { get; }
    public string RawSubscriptionPath { get; }

    public SettingsStore()
    {
        AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClashWpf");
        RunDir = Path.Combine(AppDataDir, "run");
        CoreDir = Path.Combine(AppDataDir, "core");
        SettingsPath = Path.Combine(AppDataDir, "settings.json");
        ConfigPath = Path.Combine(RunDir, "config.yaml");
        RawSubscriptionPath = Path.Combine(RunDir, "subscription.yaml");

        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(RunDir);
        Directory.CreateDirectory(CoreDir);
        Load();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var s = JsonSerializer.Deserialize<AppSettings>(json);
            if (s != null) Settings = s;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("读取设置失败: " + ex.Message);
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
