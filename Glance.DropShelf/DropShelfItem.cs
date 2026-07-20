namespace Glance.DropShelf;

public sealed record DropShelfItem(
    string Path,
    string Name,
    bool IsFolder)
{
    public string Glyph => IsFolder ? "\uE8B7" : "\uE8A5";
}
