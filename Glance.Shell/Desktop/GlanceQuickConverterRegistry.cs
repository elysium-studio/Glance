using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceQuickConverterRegistry :
    IGlanceQuickConverterRegistry
{
    private readonly Dictionary<string, IGlanceQuickConverter> converters = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly object synchronization = new();

    public IReadOnlyList<IGlanceQuickConverter> GetConverters(IReadOnlyList<GlanceStorageItem> items)
    {
        lock (synchronization)
        {
            return
            [
                .. converters.Values
                    .Where(converter => converter.CanConvert(items))
                    .OrderBy(converter => converter.Descriptor.DisplayName)
            ];
        }
    }

    public void Register(IEnumerable<IGlanceQuickConverter> registrations)
    {
        IGlanceQuickConverter[] additions = [.. registrations];

        lock (synchronization)
        {
            string? duplicateId = additions
                .Select(converter => converter.Descriptor.Id)
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1 || converters.ContainsKey(group.Key))
                .Select(group => group.Key)
                .FirstOrDefault();

            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"A quick converter with the identifier '{duplicateId}' is already registered.");
            }

            foreach (IGlanceQuickConverter converter in additions)
            {
                converters.Add(converter.Descriptor.Id, converter);
            }
        }
    }
}
