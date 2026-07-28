namespace Glance.Clipboard;

public sealed record ClipboardRecord(string Id,
    string ContentHash,
    DateTimeOffset Timestamp,
    string? Text,
    string? Html,
    string? Rtf,
    byte[]? Bitmap,
    IReadOnlyList<string>? FilePaths,
    string? WebLink,
    string? ApplicationLink);
