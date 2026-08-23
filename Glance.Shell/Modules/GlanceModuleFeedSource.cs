namespace Glance.Shell;

public sealed record GlanceModuleFeedSource(string Id, string DisplayName, Uri Uri, bool IsEnabled, bool IsBuiltIn, bool AllowLocalPackages, int Priority);
