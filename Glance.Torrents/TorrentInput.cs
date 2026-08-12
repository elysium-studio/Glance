using Glance.Application.Abstractions;

namespace Glance.Torrents;

public enum TorrentInputKind
{
    TorrentFile,
    MagnetLink
}

public sealed record TorrentInput(TorrentInputKind Kind, string Value)
{
    public static bool TryCreate(GlanceContentContext context, out TorrentInput? input)
    {
        input = null;

        if (context.Kind == GlanceContentKind.FilesAndFolders)
        {
            if (context.StorageItems.Count != 1 || context.StorageItems[0].IsFolder)
            {
                return false;
            }

            GlanceStorageItem item = context.StorageItems[0];

            if (!string.Equals(Path.GetExtension(item.Path), ".torrent", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(item.Path))
            {
                return false;
            }

            input = new TorrentInput(TorrentInputKind.TorrentFile, item.Path);
            return true;
        }

        if (context.Kind is not (GlanceContentKind.Text or GlanceContentKind.WebLink) ||
            !TorrentInputValidator.IsValidMagnet(context.Content))
        {
            return false;
        }

        input = new TorrentInput(TorrentInputKind.MagnetLink, context.Content!.Trim());
        return true;
    }
}
