using System;
using System.Linq;
using System.Windows;

namespace CanvasDesigner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 无界面自测通道：dotnet run -- --selftest
            if (e.Args != null && e.Args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                int code = Services.SelfTest.Run();
                Shutdown(code);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
