using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Glance.Shell.WinUI;

internal static class GlanceModuleLoader
{
    public static IReadOnlyList<IGlanceModule> Load()
    {
        List<IGlanceModule> modules = [];

        foreach (string path in Directory.EnumerateFiles(AppContext.BaseDirectory, "Glance.*.WinUI.dll"))
        {
            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

            foreach (Type type in GetLoadableTypes(assembly)
                .Where(type => !type.IsAbstract && typeof(IGlanceModule).IsAssignableFrom(type)))
            {
                if (Activator.CreateInstance(type) is IGlanceModule module)
                {
                    modules.Add(module);
                }
            }
        }

        return modules;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
