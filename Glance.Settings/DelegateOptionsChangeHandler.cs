using Elysium.Application;

namespace Glance.Settings;

internal sealed class DelegateOptionsChangeHandler<TOptions>(IServiceProvider provider, Action<IServiceProvider, TOptions, string?> handler) :
    IOptionsChangeHandler<TOptions>
    where TOptions : class, new()
{
    public void Handle(TOptions options, string? name) => handler(provider, options, name);
}
