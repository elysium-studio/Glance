using Glance.Application.Abstractions;

namespace Glance.Archive.WinUI;

internal sealed record ArchiveJob(GlanceContentContext Content,
    ArchiveOperationOptions Options,
    long Generation,
    CancellationToken CancellationToken);
