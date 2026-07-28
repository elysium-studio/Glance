namespace Glance.VoiceNotes.Tests;

public sealed class VoiceNoteRepositoryTests
{
    [Fact]
    public void SaveLoadAndRemove_PreserveVoiceNoteMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Glance.VoiceNotes.Tests.{Guid.NewGuid():N}");
        string databasePath = Path.Combine(directory, "voice-notes.db");

        try
        {
            VoiceNoteRepository repository = new(databasePath);
            VoiceNote older = new(Path.Combine(directory, "older.wav"), DateTimeOffset.UtcNow.AddMinutes(-2), TimeSpan.FromSeconds(12));
            VoiceNote newer = new(Path.Combine(directory, "newer.wav"), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(34));

            repository.Save(older);
            repository.Save(newer);

            Assert.Equal([newer, older], new VoiceNoteRepository(databasePath).Load());

            repository.Remove(newer.FilePath);

            Assert.Equal([older], repository.Load());
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
