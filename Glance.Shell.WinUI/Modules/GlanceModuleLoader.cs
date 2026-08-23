using DynamicXaml.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace Glance.Shell.WinUI;

internal sealed record GlanceModuleLoadResult(string SourcePath,
    string ContentDirectory,
    string? Version,
    IReadOnlyList<IGlanceModule> Modules);

internal static partial class GlanceModuleLoader
{
    private const string ModulesDirectoryName = "Modules";
    private static readonly object synchronization = new();
    private static readonly HashSet<Assembly> nativeResolverAssemblies = [];
    private static readonly Dictionary<string, nint> nativeLibraryHandles = [with(StringComparer.OrdinalIgnoreCase)];
    private static readonly List<object> xamlMetadataProviderTokens = [];
    private static Dictionary<string, string[]> moduleAssemblyPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private static Dictionary<string, string[]> moduleNativeLibraryPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private static IReadOnlyList<ModuleSource>? startupSources;
    private static bool resolverRegistered;

    public static string UserModulesDirectory => GlanceModuleInstallationStore.RootDirectory;

    public static string[] ModuleDirectories => [UserModulesDirectory];

    public static void Initialize()
    {
        IReadOnlyList<ModuleSource> sources = DiscoverSources();
        RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory));
        RegisterResolver();

        lock (synchronization)
        {
            startupSources = sources;
        }
    }

    public static IEnumerable<GlanceModuleLoadResult> Load(IReadOnlyList<string>? preferredPackageOrder = null)
    {
        IReadOnlyList<ModuleSource> sources = OrderSources(GetStartupSources(), preferredPackageOrder);

        foreach (ModuleSource source in sources)
        {
            GlanceModuleLoadResult result = Load(source, false);

            if (result.Modules.Count > 0)
            {
                yield return result;
            }
        }
    }

    private static IReadOnlyList<ModuleSource> OrderSources(IReadOnlyList<ModuleSource> sources,
        IReadOnlyList<string>? preferredPackageOrder)
    {
        if (preferredPackageOrder is null || preferredPackageOrder.Count == 0)
        {
            return sources;
        }

        Dictionary<string, int> order = preferredPackageOrder
            .Select((id, index) => (id, index))
            .GroupBy(item => item.id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        return (ModuleSource[])[.. sources
            .OrderBy(source => order.GetValueOrDefault(Path.GetFileNameWithoutExtension(source.SourcePath), int.MaxValue))
            .ThenBy(source => source.SourcePath, StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyList<ModuleSource> GetStartupSources()
    {
        IReadOnlyList<ModuleSource>? sources;

        lock (synchronization)
        {
            sources = startupSources;
            startupSources = null;
        }

        if (sources is null)
        {
            sources = DiscoverSources();
            RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory), true);
            RegisterResolver();
        }

        return sources;
    }

    public static GlanceModuleLoadResult? LoadPackage(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        string contentDirectory = PrepareInstalledPackage(fullPackagePath);

        RegisterAssemblyPaths((string[])[contentDirectory]);
        RegisterResolver();

        GlanceModuleLoadResult result = Load(new ModuleSource(fullPackagePath, contentDirectory), true);
        return result.Modules.Count > 0 ? result : null;
    }

    public static void RefreshResolutionPaths(IEnumerable<string> contentDirectories) => RegisterAssemblyPaths(contentDirectories, true);

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
        }

        return (ModuleSource[])[.. sources
            .GroupBy(source => source.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];
    }

    private static IEnumerable<string> GetModuleDirectories() => ModuleDirectories;

    private static string? PreparePackage(string packagePath)
    {
        try
        {
            return PrepareInstalledPackage(packagePath);
        }
        catch
        {
            return null;
        }
    }

    private static GlanceModuleLoadResult Load(ModuleSource source,
        bool throwOnFailure)
    {
        List<IGlanceModule> modules = [];

        IEnumerable<string> resourceModules = Directory.EnumerateFiles(source.ContentDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(path => File.Exists(Path.ChangeExtension(path, ".pri")));
        IEnumerable<string> headlessModules = Directory.EnumerateFiles(source.ContentDirectory, "*.deps.json", SearchOption.TopDirectoryOnly)
            .Select(path => path[..^".deps.json".Length] + ".dll")
            .Where(File.Exists);

        foreach (string path in resourceModules.Concat(headlessModules).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            modules.AddRange(LoadAssembly(path, throwOnFailure));
        }

        return new GlanceModuleLoadResult(source.SourcePath, source.ContentDirectory, ReadPackageVersion(source.ContentDirectory), modules);
    }

    private static string? ReadPackageVersion(string contentDirectory)
    {
        string manifestPath = Path.Combine(contentDirectory, "module.json");

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            string? version = manifest.RootElement.TryGetProperty("version", out JsonElement value) ? value.GetString() : null;
            return Version.TryParse(version, out _) ? version : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static IReadOnlyList<IGlanceModule> LoadAssembly(string path,
        bool throwOnFailure)
    {
        List<IGlanceModule> modules = [];

        try
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);
            Assembly? existingAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
                AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName));

            string priPath = Path.ChangeExtension(path, ".pri");

            if (existingAssembly is null && File.Exists(priPath) && !DynamicLoader.TryLoadPri(priPath))
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException($"The module PRI resource for '{Path.GetFileName(path)}' could not be loaded.");
                }

                return modules;
            }

            Assembly assembly = existingAssembly ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

            if (existingAssembly is null)
            {
                RegisterNativeLibraryResolver(assembly);
                RegisterXamlMetadataProviders(assembly);
            }

            foreach (Type type in GetLoadableTypes(assembly).Where(type => !type.IsAbstract && typeof(IGlanceModule).IsAssignableFrom(type)))
            {
                if (Activator.CreateInstance(type) is IGlanceModule module)
                {
                    modules.Add(module);
                }
            }
        }
        catch when (!throwOnFailure)
        {
        }

        return modules;
    }

    private static void RegisterAssemblyPaths(IEnumerable<string> contentDirectories,
        bool replaceExisting = false)
    {
        Dictionary<string, List<string>> assemblyPaths;
        Dictionary<string, List<string>> nativeLibraryPaths;

        lock (synchronization)
        {
            assemblyPaths = replaceExisting
                ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                : moduleAssemblyPaths.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);
            nativeLibraryPaths = replaceExisting
                ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                : moduleNativeLibraryPaths.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);
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
                        AddResolutionPath(assemblyPaths, assemblyName, path);
                    }
                }
                catch (BadImageFormatException)
                {
                    AddResolutionPath(nativeLibraryPaths, Path.GetFileName(path), path);
                }
                catch (FileLoadException)
                {
                }
            }
        }

        lock (synchronization)
        {
            moduleAssemblyPaths = assemblyPaths.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            moduleNativeLibraryPaths = nativeLibraryPaths.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void AddResolutionPath(Dictionary<string, List<string>> paths, string name, string path)
    {
        if (!paths.TryGetValue(name, out List<string>? candidates))
        {
            candidates = [];
            paths.Add(name, candidates);
        }

        if (!candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(path);
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
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveModuleNativeLibraryFromLoadContext;

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(WinRT.ActivationFactory).Assembly, ResolveModuleNativeLibrary);
            }
            catch (InvalidOperationException)
            {
            }

            resolverRegistered = true;
        }
    }

    private static string PrepareInstalledPackage(string packagePath)
    {
        string packageDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath))!;
        ModulePackageCache packageCache = new(Path.Combine(packageDirectory, "Runtime"));
        return packageCache.Prepare(packagePath);
    }

    private static Assembly? ResolveModuleAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        IReadOnlyDictionary<string, string[]> assemblyPaths = moduleAssemblyPaths;

        if (assemblyName.Name is null || !assemblyPaths.TryGetValue(assemblyName.Name, out string[]? paths))
        {
            return null;
        }

        foreach (string path in paths.Where(File.Exists))
        {
            try
            {
                if (!AssemblyName.ReferenceMatchesDefinition(assemblyName, AssemblyName.GetAssemblyName(path)))
                {
                    continue;
                }

                Assembly assembly = context.LoadFromAssemblyPath(path);
                RegisterNativeLibraryResolver(assembly);
                return assembly;
            }
            catch (BadImageFormatException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (FileNotFoundException)
            {
            }
        }

        return null;
    }

    private static void RegisterNativeLibraryResolver(Assembly assembly)
    {
        if (!string.Equals(assembly.GetName().Name, "Microsoft.ML.OnnxRuntimeGenAI", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (synchronization)
        {
            if (!nativeResolverAssemblies.Add(assembly))
            {
                return;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(assembly, ResolveModuleNativeLibrary);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static nint ResolveModuleNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string? assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        string fileName = Path.HasExtension(libraryName) ? libraryName : $"{libraryName}.dll";

        lock (synchronization)
        {
            string? path = assemblyDirectory is null ? null : Path.Combine(assemblyDirectory, fileName);

            if (path is null || !File.Exists(path))
            {
                if (!moduleNativeLibraryPaths.TryGetValue(fileName, out string[]? paths))
                {
                    return 0;
                }

                path = paths.FirstOrDefault(File.Exists);

                if (path is null)
                {
                    return 0;
                }
            }

            if (nativeLibraryHandles.TryGetValue(path, out nint existingHandle))
            {
                return existingHandle;
            }

            if (string.Equals(fileName, "onnxruntime-genai.dll", StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadNativeLibrary(Path.Combine(Path.GetDirectoryName(path)!, "onnxruntime.dll"));
            }

            return LoadNativeLibrary(path);
        }
    }

    private static nint ResolveModuleNativeLibraryFromLoadContext(Assembly assembly, string libraryName) => ResolveModuleNativeLibrary(libraryName, assembly, null);

    private static nint LoadNativeLibrary(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        nint handle = NativeMethods.LoadLibraryEx(path, 0, 0x00000100 | 0x00001000);

        if (handle == 0)
        {
            return 0;
        }

        nativeLibraryHandles[path] = handle;
        return handle;
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

    private sealed record ModuleSource(string SourcePath,
        string ContentDirectory);

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial nint LoadLibraryEx(string fileName, nint file, uint flags);
    }
}
