using System.Collections.ObjectModel;
using System.IO;
using Caliburn.Micro;
using ClashWpf.Models;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>日志页：引擎 stdout/stderr 实时显示（内存环，最多 800 行）。</summary>
public class LogsViewModel : Screen, IHandle<LogLineMessage>
{
    private readonly IClashEngine _engine;
    private readonly IEventAggregator _events;

    public ObservableCollection<LogEntry> Lines { get; } = new();

    public LogsViewModel(IClashEngine engine, IEventAggregator events)
    {
        _engine = engine;
        _events = events;
        _events.SubscribeOnUIThread(this);

        // 载入历史缓冲
        foreach (var l in _engine.LogBuffer.ToList().TakeLast(800))
            Lines.Add(new LogEntry(l));
    }

    public Task HandleAsync(LogLineMessage message, CancellationToken cancellationToken)
    {
        Lines.Add(new LogEntry(message.Line) { IsError = message.IsError });
        while (Lines.Count > 800) Lines.RemoveAt(0);
        NotifyOfPropertyChange(() => CountText);
        return Task.CompletedTask;
    }

    public string CountText => $"{Lines.Count} 行";

    public void Clear()
    {
        Lines.Clear();
        NotifyOfPropertyChange(() => CountText);
    }

    /// <summary>用资源管理器打开 engine.log 所在目录</summary>
    public void OpenLogFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(_engine.LogFilePath);
            if (Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_engine.LogFilePath}\"");
        }
        catch { /* ignore */ }
    }
}

public class LogEntry
{
    public string Text { get; }
    public bool IsError { get; set; }

    public LogEntry(string text) => Text = text;
}
