namespace Glance.Inspector.FileSystem;

public interface IFolderSpaceAnalyzer
{
    Task<FolderSpaceAnalysis> AnalyzeAsync(string path, CancellationToken cancellationToken);
}
