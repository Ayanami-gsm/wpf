using System.Windows.Threading;
using Caliburn.Micro;
using ClashWpf.Services;

namespace ClashWpf.ViewModels;

/// <summary>
/// 全局 UI 事件桥：ClashEngine/后台线程通过 EngineEvents.PostHandler 提交消息，
/// 这里统一切到 WPF Dispatcher 后经 IEventAggregator 广播给各 ViewModel（async 语义）。
/// </summary>
public static class UiBridge
{
    public static void Init(IEventAggregator aggregator, Dispatcher dispatcher)
    {
        EngineEvents.PostHandler = msg =>
        {
            System.Action fire = () =>
            {
                _ = aggregator.PublishOnCurrentThreadAsync(msg);
            };
            if (dispatcher.CheckAccess())
                fire();
            else
                dispatcher.BeginInvoke(fire);
        };
    }
}
