using System.Collections.Generic;

namespace Glance.Clipboard.WinUI;

internal sealed class ClipboardSnapshot
{
    public string? ApplicationLink { get; internal set; }

    public byte[]? Bitmap { get; internal set; }

    public IReadOnlyList<string>? FilePaths { get; internal set; }

    public string? Html { get; internal set; }

    public string? Rtf { get; internal set; }

    public string? Text { get; internal set; }

    public string? WebLink { get; internal set; }

    public bool HasContent =>
        ApplicationLink is not null ||
        Bitmap is not null ||
        FilePaths is { Count: > 0 } ||
        Html is not null ||
        Rtf is not null ||
        Text is not null ||
        WebLink is not null;
}
