using Glance.Shell;
using System.IO.Compression;
using Xunit;

namespace Glance.Tests;

public sealed class ModulePackageCacheTests
{
    [Fact]
    public void PrepareExtractsPackageContents()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string packagePath = CreatePackage(temporaryDirectory.DirectoryPath, ("Example.WinUI.dll", "assembly"), ("Resources/Example.WinUI.pri", "resources"));
        ModulePackageCache cache = new(Path.Combine(temporaryDirectory.DirectoryPath, "Cache"));

        string contentDirectory = cache.Prepare(packagePath);

        Assert.Equal("assembly", File.ReadAllText(Path.Combine(contentDirectory, "Example.WinUI.dll")));
        Assert.Equal("resources", File.ReadAllText(Path.Combine(contentDirectory, "Resources", "Example.WinUI.pri")));
    }

    [Fact]
    public void PrepareReusesUnchangedPackageCache()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string packagePath = CreatePackage(temporaryDirectory.DirectoryPath, ("Example.WinUI.dll", "assembly"));
        ModulePackageCache cache = new(Path.Combine(temporaryDirectory.DirectoryPath, "Cache"));
        string contentDirectory = cache.Prepare(packagePath);
        string markerPath = Path.Combine(contentDirectory, "marker.txt");
        File.WriteAllText(markerPath, "preserved");

        string reusedContentDirectory = cache.Prepare(packagePath);

        Assert.Equal(contentDirectory, reusedContentDirectory);
        Assert.Equal("preserved", File.ReadAllText(markerPath));
    }

    [Fact]
    public void PrepareExtractsUpdatedPackage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string packagePath = CreatePackage(temporaryDirectory.DirectoryPath, ("Example.WinUI.dll", "first"));
        ModulePackageCache cache = new(Path.Combine(temporaryDirectory.DirectoryPath, "Cache"));
        string firstContentDirectory = cache.Prepare(packagePath);

        File.Delete(packagePath);
        CreatePackage(temporaryDirectory.DirectoryPath, ("Example.WinUI.dll", "updated"));
        File.SetLastWriteTimeUtc(packagePath, DateTime.UtcNow.AddSeconds(2));
        string updatedContentDirectory = cache.Prepare(packagePath);

        Assert.NotEqual(firstContentDirectory, updatedContentDirectory);
        Assert.Equal("updated", File.ReadAllText(Path.Combine(updatedContentDirectory, "Example.WinUI.dll")));
    }

    [Fact]
    public void PrepareRejectsEntriesOutsideExtractionDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string packagePath = CreatePackage(temporaryDirectory.DirectoryPath, ("../outside.txt", "invalid"));
        ModulePackageCache cache = new(Path.Combine(temporaryDirectory.DirectoryPath, "Cache"));

        Assert.Throws<InvalidDataException>(() => cache.Prepare(packagePath));
        Assert.False(File.Exists(Path.Combine(temporaryDirectory.DirectoryPath, "outside.txt")));
    }

    private static string CreatePackage(string directory, params (string Path, string Content)[] entries)
    {
        string packagePath = Path.Combine(directory, "Example.glance");

        using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        foreach ((string path, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);

            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }

        return packagePath;
    }

    private sealed class TemporaryDirectory :
        IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "Glance.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath
        {
            get;
        }

        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}
