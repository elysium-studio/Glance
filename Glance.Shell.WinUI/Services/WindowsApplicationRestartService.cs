using Elysium.Application.Abstractions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public sealed class WindowsApplicationRestartService(IApplicationLifetime applicationLifetime) :
    IApplicationRestartService
{
    private readonly IApplicationLifetime applicationLifetime = applicationLifetime;

    public async Task RestartAsync()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The Glance executable path is not available.");
        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--restart-after");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Glance could not start the replacement process.");
        await applicationLifetime.ExitAsync();
    }
}
