using Elysium.Presentation.Abstractions;

namespace Glance.Tests;

internal sealed class TestNavigator :
    INavigator
{
    public Task NavigateAsync(string key, object?[]? args = null, NavigationParameters? parameters = null) => Task.CompletedTask;
}
