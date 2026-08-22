using Elysium.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Glance.Settings;

internal sealed class GlanceSettingsOptionsMonitor<TOptions>(IWritableOptions<TOptions> options) :
    IOptionsMonitor<TOptions>,
    IGlanceSettingsChangePublisher<TOptions>
    where TOptions : class, new()
{
    private readonly Lock sync = new();
    private readonly List<Action<TOptions, string?>> listeners = [];

    public TOptions CurrentValue => options.ReadAsync().GetAwaiter().GetResult() ?? new TOptions();

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TOptions, string?> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        lock (sync)
        {
            listeners.Add(listener);
        }

        return new GlanceSettingsChangeSubscription(() => Remove(listener));
    }

    public void Publish(TOptions settings)
    {
        Action<TOptions, string?>[] current;

        lock (sync)
        {
            current = [.. listeners];
        }

        foreach (Action<TOptions, string?> listener in current)
        {
            listener(settings, null);
        }
    }

    private void Remove(Action<TOptions, string?> listener)
    {
        lock (sync)
        {
            _ = listeners.Remove(listener);
        }
    }
}
