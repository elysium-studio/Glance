using Glance.Application.Abstractions;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.IO;

namespace Glance.UI.WinUI;

public sealed class ModuleResourceTextLocalizer<TMarker> :
    ITextLocalizer
{
    private readonly ResourceLoader resourceLoader;

    public ModuleResourceTextLocalizer()
    {
        string assemblyName = typeof(TMarker).Assembly.GetName().Name ??
            throw new InvalidOperationException("The module assembly has no name.");
        string resourcePath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.pri");

        resourceLoader = new ResourceLoader(resourcePath, $"{assemblyName}/Resources");
    }

    public string GetText(string key, params object[] arguments)
    {
        string value = resourceLoader.GetString(key);

        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }
}
