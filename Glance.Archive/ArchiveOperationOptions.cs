namespace Glance.Archive;

public sealed record ArchiveOperationOptions(ArchiveOperation Operation,
    ArchiveFormat Format,
    ArchiveCompressionLevel CompressionLevel);
