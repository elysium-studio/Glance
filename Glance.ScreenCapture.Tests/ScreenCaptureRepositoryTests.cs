namespace Glance.ScreenCapture.Tests;

public sealed class ScreenCaptureRepositoryTests
{
    [Fact]
    public void SaveLoadAndRemove_PreserveCaptureMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.ScreenCapture.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "screen-captures.db");

        try
        {
            ScreenCaptureRepository repository = new(databasePath);
            ScreenCaptureItem region = new(Path.Combine(directory, "region.png"), "region.png", DateTimeOffset.UtcNow.AddMinutes(-1), 800, 600, ScreenCaptureMode.Region);
            ScreenCaptureItem window = new(Path.Combine(directory, "window.png"), "window.png", DateTimeOffset.UtcNow, 1920, 1080, ScreenCaptureMode.Window);

            repository.Save(region);
            repository.Save(window);

            Assert.Equal([window, region], new ScreenCaptureRepository(databasePath).Load());

            repository.Remove(window.FilePath);

            Assert.Equal([region], repository.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
