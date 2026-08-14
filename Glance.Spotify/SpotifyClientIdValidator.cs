namespace Glance.Spotify;

public static class SpotifyClientIdValidator
{
    public static bool IsValid(string? clientId)
    {
        string value = clientId?.Trim() ?? string.Empty;

        if (value.Length is < 16 or > 128)
        {
            return false;
        }

        return value.All(character => char.IsAsciiLetterOrDigit(character));
    }
}
