using Glance.Application.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace Glance.Inspector.FileSystem;

internal sealed class CopyPathInspectionAction(string path, ModuleResourceTextLocalizer<FileSystemInspectorModule> localizer) :
    IGlanceInspectionAction
{
    public string Id => "Inspector.CopyPath";

    public string DisplayName => localizer.GetText("CopyPath");

    public string Glyph => "\uE8C8";

    public bool IsDestructive => false;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        DataPackage content = new();
        content.SetText(path);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Clipboard.SetContent(content);
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(50 * (attempt + 1), cancellationToken);
            }
        }
    }
}
