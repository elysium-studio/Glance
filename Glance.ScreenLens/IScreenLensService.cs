namespace Glance.ScreenLens;

public interface IScreenLensService
{
    Task<ScreenLensResult?> ExtractAsync();

    Task<bool> CopyAsync(string text);
}
