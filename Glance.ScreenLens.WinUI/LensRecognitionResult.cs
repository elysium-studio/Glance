namespace Glance.ScreenLens.WinUI;

internal sealed record LensRecognitionResult(string Text,
    IReadOnlyList<LensRecognizedWord> Words)
{
    public static LensRecognitionResult Empty { get; } = new(string.Empty, []);
}

internal sealed record LensRecognizedWord(string Text,
    LensRectangle Bounds,
    int LineIndex,
    int WordIndex);

internal sealed record LensSelectionResult(LensRectangle Bounds,
    LensSelectionWindow Overlay);
