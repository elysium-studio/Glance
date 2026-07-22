namespace Glance.Application.Abstractions;

public sealed class GlanceModuleOptions<TOptions>(TOptions current)
    where TOptions : class, new()
{
    public TOptions Current { get; private set; } = current;

    public event EventHandler<GlanceModuleOptionsChangedEventArgs<TOptions>>? Changed;

    public void Update(TOptions options)
    {
        Current = options;
        Changed?.Invoke(this, new GlanceModuleOptionsChangedEventArgs<TOptions>(options));
    }
}

public sealed class GlanceModuleOptionsChangedEventArgs<TOptions>(TOptions options) :
    EventArgs
    where TOptions : class, new()
{
    public TOptions Options { get; } = options;
}
