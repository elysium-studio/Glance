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
    private const string ModulesDirectoryName = "Modules";
    private static IReadOnlyDictionary<string, string> moduleAssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static bool resolverRegistered;

    public static IReadOnlyList<IGlanceModule> Load()
    {
        List<IGlanceModule> modules = [];
        string modulesDirectory = Path.Combine(AppContext.BaseDirectory, ModulesDirectoryName);

        if (!Directory.Exists(modulesDirectory))
        {
            return modules;
        }

        string[] assemblyPaths = Directory.EnumerateFiles(modulesDirectory, "*.dll", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        moduleAssemblyPaths = assemblyPaths.ToDictionary(path => AssemblyName.GetAssemblyName(path).Name!, StringComparer.OrdinalIgnoreCase);
        RegisterResolver();

        foreach (string path in assemblyPaths.Where(path => Path.GetFileName(path).StartsWith("Glance.", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).EndsWith(".WinUI.dll", StringComparison.OrdinalIgnoreCase)))
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);
            Assembly assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName)) ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

            foreach (Type type in GetLoadableTypes(assembly).Where(type => !type.IsAbstract && typeof(IGlanceModule).IsAssignableFrom(type)))
            {
                if (Activator.CreateInstance(type) is IGlanceModule module)
                {
                    modules.Add(module);
                }
            }
        }

        return modules;
    }

    private static void RegisterResolver()
    {
        if (resolverRegistered)
        {
            return;
        }

        AssemblyLoadContext.Default.Resolving += ResolveModuleAssembly;
        resolverRegistered = true;
    }

    private static Assembly? ResolveModuleAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name is null || !moduleAssemblyPaths.TryGetValue(assemblyName.Name, out string? path))
        {
            return null;
        }

        return context.LoadFromAssemblyPath(path);
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
