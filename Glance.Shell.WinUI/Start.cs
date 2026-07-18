using System;

namespace Glance.Shell.WinUI;

public static class Start
{
    [STAThread]
    public static void Main()
    {
#pragma warning disable CA1806
        Microsoft.UI.Xaml.Application.Start(_ => new App());
#pragma warning restore CA1806
    }
}
