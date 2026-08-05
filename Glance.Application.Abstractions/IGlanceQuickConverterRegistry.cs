namespace Glance.Application.Abstractions;

public interface IGlanceQuickConverterRegistry
{
    IReadOnlyList<IGlanceQuickConverter> GetConverters(IReadOnlyList<GlanceStorageItem> items);
}
