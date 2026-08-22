using Elysium.Application;

namespace Glance.Settings;

internal sealed class DelegateAsyncOptionsChangeHandler<TOptions>(IServiceProvider provider, Func<IServiceProvider, TOptions, string?, Task> handler) :
    IAsyncOptionsChangeHandler<TOptions>
    where TOptions : class, new()
{
    public Task HandleAsync(TOptions options, string? name) => handler(provider, options, name);
}
