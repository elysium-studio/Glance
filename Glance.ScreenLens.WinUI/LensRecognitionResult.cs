namespace Glance.ScreenLens.WinUI;

internal sealed record LensRecognitionResult(string Text,
    IReadOnlyList<LensRecognizedLine> Lines,
    IReadOnlyList<LensRecognizedWord> Words)
{
    public static LensRecognitionResult Empty { get; } = new(string.Empty, [], []);
}

internal sealed record LensRecognizedLine(string Text,
    LensRectangle Bounds);

internal sealed record LensRecognizedWord(string Text,
    LensRectangle Bounds,
    int LineIndex,
    int WordIndex);
