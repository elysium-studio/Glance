using Elysium.UI.WinUI;
using Glance.Application.Abstractions;

namespace Glance.Shell.WinUI;

public sealed class ResourceTextLocalizer(IStringLocalizer localizer) :
    ITextLocalizer
{
    public string GetText(string key, params object[] arguments) =>
        localizer.GetString(key, arguments);
}
