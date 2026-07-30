namespace Glance.ScreenLens;

public sealed record ScreenLensResult(string Text,
    int LineCount,
    ScreenLensRecognitionEngine Engine);
