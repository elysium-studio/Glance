using Glance.Archive.WinUI;
using System.IO.Compression;

namespace Glance.Archive.Tests;

public sealed class ArchiveServiceTests :
    IDisposable
{
    private readonly string temporaryPath = Path.Combine(Path.GetTempPath(), "GlanceArchiveTests", Guid.NewGuid().ToString("N"));

    public ArchiveServiceTests() => Directory.CreateDirectory(temporaryPath);

    [Theory]
    [InlineData("sample.zip")]
    [InlineData("sample.7z")]
    [InlineData("sample.tar.gz")]
    [InlineData("sample.rar")]
    public void RecognizesArchiveFiles(string fileName) => Assert.True(ArchiveFile.IsArchive(fileName));

    [Theory]
    [InlineData("sample.txt")]
    [InlineData("sample.png")]
    [InlineData("sample")]
    public void RejectsNonArchiveFiles(string fileName) => Assert.False(ArchiveFile.IsArchive(fileName));

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.SevenZip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.TarGZip)]
    public async Task CreatesAndExtractsArchives(ArchiveFormat format)
    {
        string folderPath = Path.Combine(temporaryPath, "Source");
        Directory.CreateDirectory(folderPath);
        await File.WriteAllTextAsync(Path.Combine(folderPath, "One.txt"), "One");
        Directory.CreateDirectory(Path.Combine(folderPath, "Nested"));
        await File.WriteAllTextAsync(Path.Combine(folderPath, "Nested", "Two.txt"), "Two");
        ArchiveService service = new();
        ArchiveOperationOptions options = new(ArchiveOperation.Create, format, ArchiveCompressionLevel.Balanced);

        string archivePath = await service.CreateAsync([new ArchiveItem(folderPath, "Source", true)], options, null, CancellationToken.None);
        string extractedPath = await service.ExtractAsync(archivePath, null, CancellationToken.None);

        Assert.True(File.Exists(archivePath));
        Assert.Equal("One", await File.ReadAllTextAsync(Path.Combine(extractedPath, "Source", "One.txt")));
        Assert.Equal("Two", await File.ReadAllTextAsync(Path.Combine(extractedPath, "Source", "Nested", "Two.txt")));
    }

    [Fact]
    public async Task PreservesEmptyFolders()
    {
        string folderPath = Path.Combine(temporaryPath, "Source");
        Directory.CreateDirectory(Path.Combine(folderPath, "Empty"));
        ArchiveService service = new();
        ArchiveOperationOptions options = new(ArchiveOperation.Create, ArchiveFormat.Zip, ArchiveCompressionLevel.Balanced);

        string archivePath = await service.CreateAsync([new ArchiveItem(folderPath, "Source", true)], options, null, CancellationToken.None);
        string extractedPath = await service.ExtractAsync(archivePath, null, CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(extractedPath, "Source", "Empty")));
    }

    [Fact]
    public async Task RejectsArchiveEntriesOutsideDestination()
    {
        string archivePath = Path.Combine(temporaryPath, "Unsafe.zip");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("../Outside.txt");
            await using Stream stream = entry.Open();
            await using StreamWriter writer = new(stream);
            await writer.WriteAsync("Unsafe");
        }

        ArchiveService service = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(archivePath, null, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(temporaryPath, "Outside.txt")));
    }

    [Fact]
    public async Task UsesAUniqueArchiveName()
    {
        string filePath = Path.Combine(temporaryPath, "Notes.txt");
        await File.WriteAllTextAsync(filePath, "Notes");
        ArchiveService service = new();
        ArchiveOperationOptions options = new(ArchiveOperation.Create, ArchiveFormat.Zip, ArchiveCompressionLevel.Fast);
        ArchiveItem item = new(filePath, "Notes.txt", false);

        string firstPath = await service.CreateAsync([item], options, null, CancellationToken.None);
        string secondPath = await service.CreateAsync([item], options, null, CancellationToken.None);

        Assert.NotEqual(firstPath, secondPath);
        Assert.EndsWith("Notes.zip", firstPath);
        Assert.EndsWith("Notes (1).zip", secondPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryPath))
        {
            Directory.Delete(temporaryPath, true);
        }
    }
}
