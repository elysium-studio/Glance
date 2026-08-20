using Glance.Application.Abstractions;
using System.Diagnostics;

namespace Glance.Inspector.FileSystem;

internal sealed class OpenLocationInspectionAction(string path, bool isFolder, ModuleResourceTextLocalizer<FileSystemInspectorModule> localizer) :
    IGlanceInspectionAction
{
    public string Id => "Inspector.OpenLocation";

    public string DisplayName => localizer.GetText("OpenLocation");

    public string Glyph => "\uE838";

    public bool IsDestructive => false;

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo start = isFolder ? new(path) { UseShellExecute = true } : new("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true };
        _ = Process.Start(start);
        return Task.CompletedTask;
    }
}
