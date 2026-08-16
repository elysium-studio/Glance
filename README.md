# Glance

Glance is a lightweight Windows companion that keeps useful controls, live information, and quick actions close at hand. It presents each feature as a module with a compact view for everyday use and an expanded view for more detail and controls.

Modules can be enabled, disabled, reordered, and configured independently.

## Modules

Glance includes modules for:

- **Time and productivity:** Stopwatch, Timer, Focus Session, Reminders, Keep Awake, Presence, World Clock, Clipboard, Stash, Drop Shelf, and Torrent.
- **Media and capture:** Media, Audio Switcher, App Mixer, Voice Notes, Screen Capture, Screen Recorder, Screen Lens, Colour Picker, and Quick Convert.
- **System and devices:** System Monitor, System Indicators, Network Speed, Network details, Power, Privacy Controls, Removable Devices, Bluetooth Devices, Magnifier, and Theme Switcher.
- **Information:** Weather.

## Build Glance

### Requirements

- Windows 11 build 22000 or later. Windows 10 is not supported.
- Visual Studio with the .NET desktop and Windows application development workloads, or the equivalent command-line build tools.
- The .NET 11 preview SDK.
- An x64 machine or build environment.
- Access to the package sources configured in `NuGet.config`.

Clone the repository, restore the packages, and build the x64 solution:

```powershell
git clone https://github.com/elysium-studio/Glance.git
cd Glance
dotnet restore Glance.slnx
dotnet build Glance.slnx -c Debug -p:Platform=x64
```

The application project is `Glance.Shell.WinUI`. Open `Glance.slnx` in Visual Studio and select the `x64` platform to build or debug it there.

Run the tests with:

```powershell
dotnet test Glance.slnx -c Debug -p:Platform=x64
```

## Build a module

A typical module uses three projects:

```text
Glance.Example/             Models, settings, contracts, and view models
Glance.Example.WinUI/       Windows services, registration, resources, and views
Glance.Example.Tests/       Unit tests for platform-independent behaviour
```

Use an existing small module such as `Glance.Stopwatch` as a reference for the complete structure.

### 1. Create the views and component

The WinUI project targets the same framework as Glance and enables compiled WinUI resources:

```xml
<PropertyGroup>
  <TargetFramework>net11.0-windows10.0.22000.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.22000.0</TargetPlatformMinVersion>
  <UseWinUI>true</UseWinUI>
  <DisableEmbeddedXbf>false</DisableEmbeddedXbf>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  <Platforms>AnyCPU;x64</Platforms>
</PropertyGroup>
```

Implement `IGlanceComponent` and provide compact and expanded WinUI content:

```csharp
using Glance.Application.Abstractions;

public sealed class ExampleComponent : IGlanceComponent
{
    public string Id => "Example";
    public string DisplayName => "Example";
    public string Description => "A short description of the module.";
    public string SettingsCategory => GlanceModuleCategories.Productivity;
    public string IconGlyph => "\uE946";
    public int Order => 100;

    public object CompactContent { get; } = new ExampleCompactView();
    public object ExpandedContent { get; } = new ExampleExpandedView();
}
```

Keep UI text in `Strings/<language>/Resources.resw` and module brushes or styles in a theme resource dictionary. Put platform-independent state and view-model logic in the non-WinUI project so it can be tested without starting the application.

### 2. Register the module

Create a public, parameterless `IGlanceModule` implementation in the WinUI assembly. Register the component and any module-owned services with dependency injection:

```csharp
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public sealed class ExampleModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ExampleComponent>();
        services.AddSingleton<IGlanceComponent>(provider =>
            provider.GetRequiredService<ExampleComponent>());
    }
}
```

Optional contracts add more integration without shell-specific code:

- `IGlanceModuleSettingViewModel` adds module settings.
- `IGlanceAttentionComponent` allows the module to request the user's attention, subject to permission.
- `IGlanceTransientComponent` presents short-lived content without adding it to paging or module ordering.
- `IGlanceIntent` lets the module accept contextual files, text, links, or other content.
- `IGlanceActionProvider` exposes commands to the application action system.
- `IGlanceViewAwareComponent` lets a module suspend background work while it is not being shown.
- `IDisposable` or `IAsyncDisposable` provides deterministic cleanup.

Modules that poll, capture, or maintain other continuous work can opt into the view lifecycle:

```csharp
public sealed class ExampleComponent :
    IGlanceComponent,
    IGlanceViewAwareComponent
{
    public void EnterView() => refreshTimer.Start();

    public void LeaveView() => refreshTimer.Stop();
}
```

Transcription engines can be shipped as headless modules. Register an `ITranscriptionProvider` from the package's `IGlanceModule`. A provider may publish any number of `TranscriptionModel` entries and creates an `ITranscriptionDecoder` for the selected model. Glance captures the selected microphone, converts it to the provider's requested PCM format, and streams the audio into the decoder. Model downloads, model-specific preprocessing, and decoding remain owned by the provider.

### 3. Package the module

A `.glance` package is a ZIP archive whose files are stored at its root. Include the WinUI assembly and matching PRI file, the platform-independent assembly, and every private runtime dependency:

```text
Example.glance
  Glance.Example.WinUI.dll
  Glance.Example.WinUI.pri
  Glance.Example.dll
  Example.PrivateDependency.dll
```

The WinUI DLL and PRI filenames must share the same base name. A headless module instead includes its entry assembly and matching `.deps.json` file. Do not package shared Glance, WinUI, Elysium, or Microsoft Extensions assemblies supplied by the host.

One simple packaging command is:

```powershell
Compress-Archive -Path .\ModuleOutput\* -DestinationPath .\Example.zip
Rename-Item .\Example.zip Example.glance
```

Modules run with the same permissions as Glance and are not sandboxed. Only install packages from sources you trust.

### 4. Add the module

To install a completed package:

1. Open **Settings**.
2. Open **Modules**.
3. Select **Add module** and choose the `.glance` file, or drag the package onto the module page.
4. Enable and configure the module after it appears in the list.

Installed packages and their private runtime cache are stored under `%LOCALAPPDATA%\Glance\Modules`.

To ship a module as part of this repository:

1. Add its domain, WinUI, and test projects to `Glance.slnx`.
2. Add the WinUI project to `GlanceModuleProject` in `Glance.Shell.WinUI/Glance.Shell.WinUI.csproj`.
3. Add its output files and private dependencies to `GlanceModuleBuildFile` with the correct `ModuleName`.
4. Add its staging directory and `CreateGlanceModulePackage` entry.
5. Add the generated package to `GlanceModulePackage` so it is included in build, publish, and installer output.
6. Build the full solution and inspect the generated `Modules/<Name>.glance` archive before committing it.

## Licence and third-party software

See `THIRD-PARTY-NOTICES.md` for third-party components and their licences.
