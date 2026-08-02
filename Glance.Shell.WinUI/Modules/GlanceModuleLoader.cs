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

namespace Glance.Shell.WinUI;

internal sealed record GlanceModuleLoadResult(string SourcePath,
    IReadOnlyList<IGlanceModule> Modules);

internal static partial class GlanceModuleLoader
{
    private const string ModulesDirectoryName = "Modules";
    private static readonly object synchronization = new();
    private static readonly ModulePackageCache modulePackageCache = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "ModuleCache"));
    private static readonly HashSet<Assembly> nativeResolverAssemblies = [];
    private static readonly Dictionary<string, nint> nativeLibraryHandles = [with(StringComparer.OrdinalIgnoreCase)];
    private static readonly List<object> xamlMetadataProviderTokens = [];
    private static Dictionary<string, string> moduleAssemblyPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private static bool resolverRegistered;

    public static string UserModulesDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", ModulesDirectoryName);

    public static string[] ModuleDirectories =>
    [
        Path.Combine(AppContext.BaseDirectory, ModulesDirectoryName),
        UserModulesDirectory
    ];

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

        return (GlanceModuleLoadResult[])[.. sources.Select(Load).Where(result => result.Modules.Count > 0)];
    }

    public static GlanceModuleLoadResult? LoadPackage(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        string contentDirectory = modulePackageCache.Prepare(fullPackagePath);

        RegisterAssemblyPaths((string[])[contentDirectory]);
        RegisterResolver();

        GlanceModuleLoadResult result = Load(new ModuleSource(fullPackagePath, contentDirectory));
        return result.Modules.Count > 0 ? result : null;
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

        return (ModuleSource[])[.. sources
            .GroupBy(source => source.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];
    }

    private static IEnumerable<string> GetModuleDirectories()
        => ModuleDirectories;

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
            RegisterNativeLibraryResolver(assembly);
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
                        assemblyPaths[assemblyName] = path;
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
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveModuleNativeLibraryFromLoadContext;
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

        Assembly assembly = context.LoadFromAssemblyPath(path);
        RegisterNativeLibraryResolver(assembly);
        return assembly;
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

        if (assemblyDirectory is null)
        {
            return 0;
        }

        string fileName = Path.HasExtension(libraryName) ? libraryName : $"{libraryName}.dll";
        string path = Path.Combine(assemblyDirectory, fileName);

        lock (synchronization)
        {
            if (nativeLibraryHandles.TryGetValue(path, out nint existingHandle))
            {
                return existingHandle;
            }

            if (string.Equals(fileName, "onnxruntime-genai.dll", StringComparison.OrdinalIgnoreCase))
            {
                LoadNativeLibrary(Path.Combine(assemblyDirectory, "onnxruntime.dll"));
            }

            return LoadNativeLibrary(path);
        }
    }

    private static nint ResolveModuleNativeLibraryFromLoadContext(Assembly assembly, string libraryName)
        => ResolveModuleNativeLibrary(libraryName, assembly, null);

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
