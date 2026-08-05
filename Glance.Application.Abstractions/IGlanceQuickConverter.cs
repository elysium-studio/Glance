namespace Glance.Application.Abstractions;

public interface IGlanceQuickConverter
{
    GlanceQuickConverterDescriptor Descriptor { get; }

    bool CanConvert(IReadOnlyList<GlanceStorageItem> items);

    IGlanceQuickConverterEditor? CreateEditor(IReadOnlyList<GlanceStorageItem> items);

    Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
        CancellationToken cancellationToken = default);
}
