using System.IO;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using ClashWpf.Services;
using ClashWpf.ViewModels;

namespace ClashWpf;

public class AppBootstrapper : BootstrapperBase
{
    private SimpleContainer? _container;
    private static readonly string CrashLog =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClashWpf", "crash.log");

    public AppBootstrapper()
    {
        Initialize();
    }

    /// <summary>全局兜底：任何未处理异常都写入 crash.log 且不闪退，便于定位</summary>
    private static void AttachGlobalExceptionLogging()
    {
        void Dump(string kind, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLog)!);
                File.AppendAllText(CrashLog,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}\n\n");
            }
            catch { /* 日志失败忽略 */ }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Dump("AppDomain.UnhandledException", e.ExceptionObject as Exception ?? new Exception("unknown"));
        Application.Current.DispatcherUnhandledException += (_, e) =>
        {
            Dump("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // 防止闪退
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Dump("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    protected override void Configure()
    {
        _container = new SimpleContainer();

        // 基础设施
        _container.Singleton<IEventAggregator, EventAggregator>();
        _container.Singleton<IWindowManager, WindowManager>();

        // 服务
        _container.Singleton<ISettingsStore, SettingsStore>();
        _container.Singleton<ISubscriptionService, SubscriptionService>();
        _container.Singleton<IClashApiClient, ClashApiClient>();
        _container.Singleton<IClashEngine, ClashEngine>();
        _container.Singleton<ISystemProxyService, SystemProxyService>();

        // 页面 VM（单例，避免切页重建）
        _container.Singleton<ShellViewModel>();
        _container.Singleton<DashboardViewModel>();
        _container.Singleton<ProxiesViewModel>();
        _container.Singleton<ConnectionsViewModel>();
        _container.Singleton<LogsViewModel>();
        _container.Singleton<SettingsViewModel>();
    }

    protected override object GetInstance(Type service, string? key)
        => _container!.GetInstance(service, key);

    protected override IEnumerable<object> GetAllInstances(Type service)
        => _container!.GetAllInstances(service);

    protected override void BuildUp(object instance)
        => _container!.BuildUp(instance);

    protected override async void OnStartup(object sender, StartupEventArgs e)
    {
        AttachGlobalExceptionLogging();

        var aggregator = _container!.GetInstance<IEventAggregator>();
        UiBridge.Init(aggregator, Application.Current.Dispatcher);

        // 应用退出兜底：确保引擎进程被回收
        Application.Current.Exit += (_, _) =>
        {
            try { _container?.GetInstance<IClashEngine>()?.Dispose(); }
            catch { /* ignore */ }
        };

        await DisplayRootViewForAsync<ShellViewModel>();
    }
}
