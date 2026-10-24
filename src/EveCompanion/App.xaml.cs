using System.Configuration;
using System.Windows;

namespace EveCompanion;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            SelfTest.Run();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }
}
