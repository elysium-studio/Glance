namespace Glance.Clipboard.Tests;

public sealed class ClipboardRepositoryTests
{
    [Fact]
    public void SaveAndLoad_PreserveEverySupportedClipboardFormat()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.Clipboard.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "clipboard.db");

        try
        {
            ClipboardRepository repository = new(databasePath);
            ClipboardRecord record = new("entry",
                "HASH",
                DateTimeOffset.UtcNow,
                "Plain text",
                "<p>HTML</p>",
                @"{\rtf1 RTF}",
                [1, 2, 3, 4],
                [@"C:\One.txt", @"C:\Folder"],
                "https://example.com",
                "sample-app://open");

            repository.Save(record, 6);

            ClipboardRecord restored = Assert.Single(new ClipboardRepository(databasePath).Load(6));

            Assert.Equal(record.Id, restored.Id);
            Assert.Equal(record.ContentHash, restored.ContentHash);
            Assert.Equal(record.Text, restored.Text);
            Assert.Equal(record.Html, restored.Html);
            Assert.Equal(record.Rtf, restored.Rtf);
            Assert.Equal(record.Bitmap, restored.Bitmap);
            Assert.Equal(record.FilePaths, restored.FilePaths);
            Assert.Equal(record.WebLink, restored.WebLink);
            Assert.Equal(record.ApplicationLink, restored.ApplicationLink);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void PromoteTrimRemoveAndClear_UpdatePersistentHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.Clipboard.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "clipboard.db");

        try
        {
            ClipboardRepository repository = new(databasePath);

            for (int index = 0; index < 5; index++)
            {
                repository.Save(CreateRecord(index), 5);
            }

            repository.Promote("1");

            Assert.Equal(["1", "4", "3", "2", "0"], repository.Load(5).Select(record => record.Id));

            repository.Trim(3);

            Assert.Equal(["1", "4", "3"], repository.Load(5).Select(record => record.Id));

            repository.Remove("4");

            Assert.Equal(["1", "3"], repository.Load(5).Select(record => record.Id));

            repository.Clear();

            Assert.Empty(repository.Load(5));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ClipboardRecord CreateRecord(int index) => new(index.ToString(),
            $"HASH{index}",
            DateTimeOffset.UtcNow.AddMinutes(index),
            $"Text {index}",
            null,
            null,
            null,
            null,
            null,
            null);
}
