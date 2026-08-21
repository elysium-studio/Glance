namespace Glance.Inspector.FileSystem;

public sealed record FolderSpaceAnalysis(IReadOnlyList<FolderSpaceEntry> Entries, long TotalBytes, long FileCount);
