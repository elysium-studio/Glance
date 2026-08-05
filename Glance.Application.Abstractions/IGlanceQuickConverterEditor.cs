namespace Glance.Application.Abstractions;

public interface IGlanceQuickConverterEditor
{
    object Content { get; }

    bool TryCreateOptions(out object? options,
        out string? errorMessage);
}
