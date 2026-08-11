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
    string ContentDirectory,
    IReadOnlyList<IGlanceModule> Modules);

internal static partial class GlanceModuleLoader
{
    private const string ModulesDirectoryName = "Modules";
    private static readonly object synchronization = new();
    private static readonly HashSet<Assembly> nativeResolverAssemblies = [];
    private static readonly Dictionary<string, nint> nativeLibraryHandles = [with(StringComparer.OrdinalIgnoreCase)];
    private static readonly List<object> xamlMetadataProviderTokens = [];
    private static Dictionary<string, string> moduleAssemblyPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private static Dictionary<string, string> moduleNativeLibraryPaths = [with(StringComparer.OrdinalIgnoreCase)];
    private static bool resolverRegistered;

    public static string UserModulesDirectory => GlanceModuleInstallationStore.RootDirectory;

    public static string[] ModuleDirectories => [UserModulesDirectory];

    public static void Initialize()
    {
        IReadOnlyList<ModuleSource> sources = DiscoverSources();
        RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory));
        RegisterResolver();
    }

    public static IReadOnlyList<GlanceModuleLoadResult> Load()
    {
        IReadOnlyList<ModuleSource> sources = DiscoverSources();
        RegisterAssemblyPaths(sources.Select(source => source.ContentDirectory), true);
        RegisterResolver();

        return (GlanceModuleLoadResult[])[.. sources.Select(Load).Where(result => result.Modules.Count > 0)];
    }

    public static GlanceModuleLoadResult? LoadPackage(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        string contentDirectory = PrepareInstalledPackage(fullPackagePath);

        RegisterAssemblyPaths((string[])[contentDirectory]);
        RegisterResolver();

        GlanceModuleLoadResult result = Load(new ModuleSource(fullPackagePath, contentDirectory));
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

            foreach (string priPath in Directory.EnumerateFiles(modulesDirectory, "*.pri", SearchOption.AllDirectories)
                .Where(path => !IsRuntimePath(path))
                .Order(StringComparer.OrdinalIgnoreCase))
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

    private static IEnumerable<string> GetModuleDirectories() => ModuleDirectories;

    private static bool IsRuntimePath(string path) => Path.GetFullPath(path)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains("Runtime", StringComparer.OrdinalIgnoreCase);

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

    private static GlanceModuleLoadResult Load(ModuleSource source)
    {
        List<IGlanceModule> modules = [];

        foreach (string path in Directory.EnumerateFiles(source.ContentDirectory, "*.dll", SearchOption.AllDirectories).Where(path => File.Exists(Path.ChangeExtension(path, ".pri"))).Order(StringComparer.OrdinalIgnoreCase))
        {
            modules.AddRange(LoadAssembly(path));
        }

        return new GlanceModuleLoadResult(source.SourcePath, source.ContentDirectory, modules);
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

    private static void RegisterAssemblyPaths(IEnumerable<string> contentDirectories,
        bool replaceExisting = false)
    {
        Dictionary<string, string> assemblyPaths;
        Dictionary<string, string> nativeLibraryPaths;

        lock (synchronization)
        {
            assemblyPaths = replaceExisting
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(moduleAssemblyPaths, StringComparer.OrdinalIgnoreCase);
            nativeLibraryPaths = replaceExisting
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(moduleNativeLibraryPaths, StringComparer.OrdinalIgnoreCase);
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
                    nativeLibraryPaths[Path.GetFileName(path)] = path;
                }
                catch (FileLoadException)
                {
                }
            }
        }

        lock (synchronization)
        {
            moduleAssemblyPaths = assemblyPaths;
            moduleNativeLibraryPaths = nativeLibraryPaths;
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
        string fileName = Path.HasExtension(libraryName) ? libraryName : $"{libraryName}.dll";

        lock (synchronization)
        {
            string? path = assemblyDirectory is null ? null : Path.Combine(assemblyDirectory, fileName);

            if ((path is null || !File.Exists(path)) && !moduleNativeLibraryPaths.TryGetValue(fileName, out path))
            {
                return 0;
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
