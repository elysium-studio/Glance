using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class BackgroundDownloadManagerTests
{
    [Fact]
    public async Task LocalFileCompletesAfterReleasingTemporaryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.Tests.{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.glance");
        string destination = Path.Combine(directory, "destination.glance");
        await File.WriteAllTextAsync(source, "module");

        try
        {
            using BackgroundDownloadManager manager = new(new HttpClient());
            _ = manager.Enqueue(new BackgroundDownloadRequest("local", new Uri(source), destination));
            BackgroundDownloadSnapshot result = await manager.WaitForCompletionAsync("local");

            Assert.Equal(BackgroundDownloadStatus.Completed, result.Status);
            Assert.Equal("module", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
