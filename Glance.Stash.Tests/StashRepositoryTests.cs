namespace Glance.Stash.Tests;

public sealed class StashRepositoryTests
{
    [Fact]
    public void Save_LoadAndRemove_PersistItemsInSQLite()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.Stash.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "stash.db");

        try
        {
            StashRepository repository = new(databasePath);
            StashEntry older = new("older", StashItemKind.Text, "Remember this", DateTimeOffset.UtcNow.AddMinutes(-1));
            StashEntry newer = new("newer", StashItemKind.Link, "https://github.com/elysium-studio", DateTimeOffset.UtcNow);

            repository.Save(older);
            repository.Save(newer);

            IReadOnlyList<StashEntry> restored = new StashRepository(databasePath).Load();

            Assert.Equal(["newer", "older"], restored.Select(entry => entry.Id));
            Assert.Equal(StashItemKind.Link, restored[0].Kind);

            repository.Remove(newer.Id);

            Assert.Equal(["older"], repository.Load().Select(entry => entry.Id));
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
    public void Save_KeepsOnlyTheMostRecentTwentyFourItems()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.Stash.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "stash.db");

        try
        {
            StashRepository repository = new(databasePath);
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;

            for (int index = 0; index < 30; index++)
            {
                repository.Save(new StashEntry(index.ToString(), StashItemKind.Text, $"Item {index}", createdAt.AddSeconds(index)));
            }

            IReadOnlyList<StashEntry> restored = repository.Load();

            Assert.Equal(24, restored.Count);
            Assert.Equal("29", restored[0].Id);
            Assert.Equal("6", restored[^1].Id);
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
