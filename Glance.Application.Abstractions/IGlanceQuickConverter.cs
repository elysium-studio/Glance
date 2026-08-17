namespace Glance.Application.Abstractions;

public interface IGlanceQuickConverter
{
    GlanceQuickConverterDescriptor Descriptor { get; }

    GlanceQuickConverterMatch Match(GlanceContentContext context);

    IGlanceQuickConverterEditor? CreateEditor(GlanceContentContext context);

    Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
        CancellationToken cancellationToken = default);
}
