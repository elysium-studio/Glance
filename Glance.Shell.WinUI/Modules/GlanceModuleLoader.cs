using DynamicXaml.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Xaml.Markup;
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
    private static readonly List<object> xamlMetadataProviderTokens = [];
    private static bool resolverRegistered;

    public static void Initialize()
    {
        Dictionary<string, string> assemblyPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string modulesDirectory in GetModuleDirectories().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string path in Directory.EnumerateFiles(modulesDirectory, "*.dll", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string? assemblyName = AssemblyName.GetAssemblyName(path).Name;

                    if (assemblyName is not null)
                    {
                        assemblyPaths.TryAdd(assemblyName, path);
                    }
                }
                catch (BadImageFormatException)
                {
                }
                catch (FileLoadException)
                {
                }
            }
        }

        moduleAssemblyPaths = assemblyPaths;
        RegisterResolver();
    }

    public static IReadOnlyList<IGlanceModule> Load()
    {
        Initialize();

        List<IGlanceModule> modules = [];

        foreach (string path in moduleAssemblyPaths.Values.Where(path => File.Exists(Path.ChangeExtension(path, ".pri"))))
        {
            modules.AddRange(Load(path));
        }

        return modules;
    }

    private static IEnumerable<string> GetModuleDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, ModulesDirectoryName);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", ModulesDirectoryName);
    }

    private static IReadOnlyList<IGlanceModule> Load(string path)
    {
        List<IGlanceModule> modules = [];

        try
        {
            if (!DynamicLoader.TryLoadPri(Path.ChangeExtension(path, ".pri")))
            {
                return modules;
            }

            AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);
            Assembly assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName)) ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            RegisterXamlMetadataProviders(assembly);

            foreach (Type type in GetLoadableTypes(assembly).Where(type => !type.IsAbstract && typeof(IGlanceModule).IsAssignableFrom(type)))
            {
                if (Activator.CreateInstance(type) is IGlanceModule module)
                {
                    modules.Add(module);
                }
            }
        }
        catch
        {
        }

        return modules;
    }

    private static void RegisterXamlMetadataProviders(Assembly assembly)
    {
        foreach (Type type in GetLoadableTypes(assembly).Where(type => !type.IsAbstract && typeof(IXamlMetadataProvider).IsAssignableFrom(type)))
        {
            if (Activator.CreateInstance(type) is IXamlMetadataProvider provider)
            {
                xamlMetadataProviderTokens.Add(DynamicLoader.RegisterXamlMetadataProvider(provider));
            }
        }
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
