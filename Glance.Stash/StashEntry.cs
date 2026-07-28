namespace Glance.Stash;

public sealed record StashEntry(string Id,
    StashItemKind Kind,
    string Content,
    DateTimeOffset CreatedAt);
