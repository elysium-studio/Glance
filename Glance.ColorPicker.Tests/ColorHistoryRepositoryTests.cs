namespace Glance.ColorPicker.Tests;

public sealed class ColorHistoryRepositoryTests
{
    [Fact]
    public void SaveLoadAndTrim_PreserveRecentDeduplicatedColors()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.ColorPicker.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "colors.db");

        try
        {
            ColorHistoryRepository repository = new(databasePath);
            ColorValue first = new(1, 2, 3);
            ColorValue second = new(4, 5, 6);
            ColorValue third = new(7, 8, 9);

            repository.Save(first, 3);
            repository.Save(second, 3);
            repository.Save(third, 3);
            repository.Save(first, 3);

            Assert.Equal([first, third, second], new ColorHistoryRepository(databasePath).Load(3));

            repository.Trim(2);

            Assert.Equal([first, third], repository.Load(3));
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
