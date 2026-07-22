using Glance.Application.Abstractions;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Glance.UI.WinUI;

public sealed class ModuleResourceTextLocalizer<TMarker> :
    ITextLocalizer
{
    private readonly ResourceLoader resourceLoader;

    public ModuleResourceTextLocalizer()
    {
        Assembly assembly = typeof(TMarker).Assembly;
        string assemblyName = assembly.GetName().Name ?? throw new InvalidOperationException("The module assembly has no name.");
        string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        string resourcePath = Path.Combine(assemblyDirectory, $"{assemblyName}.pri");

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
