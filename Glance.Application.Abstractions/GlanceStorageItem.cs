namespace Glance.Application.Abstractions;

public sealed record GlanceStorageItem(
    string Path,
    string Name,
    bool IsFolder);
