namespace Glance.SystemIndicators;

public sealed record SystemIndicatorPresentation(string Title,
    string PrimaryText,
    string SecondaryText,
    string Glyph,
    int? Level = null);
