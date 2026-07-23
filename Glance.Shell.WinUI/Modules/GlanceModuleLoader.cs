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

internal sealed record GlanceModuleLoadResult(
    string SourcePath,
    IReadOnlyList<IGlanceModule> Modules);

internal static class GlanceModuleLoader
{
    private const string ModulesDirectoryName = "Modules";
    private static readonly object synchronization = new();
    private static readonly HashSet<string> loadedSourcePaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ModulePackageCache modulePackageCache = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "ModuleCache"));
    private static readonly List<object> xamlMetadataProviderTokens = [];
    private static IReadOnlyDictionary<string, string> moduleAssemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static bool resolverRegistered;

    public static string UserModulesDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", ModulesDirectoryName);

    public static void Initialize()
    {
        IReadOnlyList<ModuleSource> sources = DiscoverSources();
        RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory));
        RegisterResolver();
    }

    public static IReadOnlyList<GlanceModuleLoadResult> Load()
    {
        IReadOnlyList<ModuleSource> sources = DiscoverSources();
        RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory));
        RegisterResolver();

        return sources.Select(Load).Where(result => result.Modules.Count > 0).ToArray();
    }

    public static GlanceModuleLoadResult? LoadPackage(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);

        lock (synchronization)
        {
            if (loadedSourcePaths.Contains(fullPackagePath))
            {
                return null;
            }
        }

        string? contentDirectory = PreparePackage(fullPackagePath);

        if (contentDirectory is null)
        {
            return null;
        }

        RegisterAssemblyPaths(new[] { contentDirectory });
        RegisterResolver();

        GlanceModuleLoadResult result = Load(new ModuleSource(fullPackagePath, contentDirectory));
        return result.Modules.Count > 0 ? result : null;
    }

    public static IReadOnlySet<string> GetLoadedSourcePaths()
    {
        lock (synchronization)
        {
            return loadedSourcePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<ModuleSource> DiscoverSources()
    {
        List<ModuleSource> sources = [];
        foreach (string modulesDirectory in GetModuleDirectories().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string packagePath in Directory.EnumerateFiles(modulesDirectory, "*.glance", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                string? contentDirectory = PreparePackage(packagePath);

                if (contentDirectory is not null)
                {
                    sources.Add(new ModuleSource(Path.GetFullPath(packagePath), contentDirectory));
                }
            }

            foreach (string priPath in Directory.EnumerateFiles(modulesDirectory, "*.pri", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                string assemblyPath = Path.ChangeExtension(priPath, ".dll");

                if (File.Exists(assemblyPath))
                {
                    string contentDirectory = Path.GetDirectoryName(assemblyPath)!;
                    sources.Add(new ModuleSource(Path.GetFullPath(assemblyPath), contentDirectory));
                }
            }
        }

        return sources
            .GroupBy(source => source.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IEnumerable<string> GetModuleDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, ModulesDirectoryName);
        yield return UserModulesDirectory;
    }

    private static string? PreparePackage(string packagePath)
    {
        try
        {
            return modulePackageCache.Prepare(packagePath);
        }
        catch
        {
            return null;
        }
    }

    private static GlanceModuleLoadResult Load(ModuleSource source)
    {
        List<IGlanceModule> modules = [];

        foreach (string path in Directory.EnumerateFiles(source.ContentDirectory, "*.dll", SearchOption.AllDirectories).Where(path => File.Exists(Path.ChangeExtension(path, ".pri"))).Order(StringComparer.OrdinalIgnoreCase))
        {
            modules.AddRange(LoadAssembly(path));
        }

        if (modules.Count > 0)
        {
            lock (synchronization)
            {
                loadedSourcePaths.Add(source.SourcePath);
            }
        }

        return new GlanceModuleLoadResult(source.SourcePath, modules);
    }

    private static IReadOnlyList<IGlanceModule> LoadAssembly(string path)
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

    private static void RegisterAssemblyPaths(IEnumerable<string> contentDirectories)
    {
        Dictionary<string, string> assemblyPaths;

        lock (synchronization)
        {
            assemblyPaths = new Dictionary<string, string>(moduleAssemblyPaths, StringComparer.OrdinalIgnoreCase);
        }

        foreach (string contentDirectory in contentDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string path in Directory.EnumerateFiles(contentDirectory, "*.dll", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
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

        lock (synchronization)
        {
            moduleAssemblyPaths = assemblyPaths;
        }
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
        lock (synchronization)
        {
            if (resolverRegistered)
            {
                return;
            }

            AssemblyLoadContext.Default.Resolving += ResolveModuleAssembly;
            resolverRegistered = true;
        }
    }

    private static Assembly? ResolveModuleAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        IReadOnlyDictionary<string, string> assemblyPaths = moduleAssemblyPaths;

        if (assemblyName.Name is null || !assemblyPaths.TryGetValue(assemblyName.Name, out string? path))
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

    private sealed record ModuleSource(
        string SourcePath,
        string ContentDirectory);
}
