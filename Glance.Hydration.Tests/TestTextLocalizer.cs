using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Hydration.Tests;

internal sealed class TestTextLocalizer :
    ITextLocalizer
{
    public string GetText(string key, params object[] arguments) => arguments.Length == 0 ? key : string.Format(CultureInfo.InvariantCulture, key + " " + string.Join(" ", arguments), arguments);
}
