# Glance

Glance is a lightweight Windows desktop companion that keeps useful controls and live information within reach without becoming another full-sized application window. It is built with WinUI 3 and centred around the [`DesktopIsland`](https://github.com/elysium-studio/Elysium) control from Elysium.

The island remains at the top or bottom of the desktop in a compact state, expands when it is needed, and allows modules to request attention when something important happens. Modules can be enabled, disabled, and reordered from Settings.

## Modules

- **Stopwatch** — start, pause, and reset an elapsed-time counter.
- **Timer** — adjust a countdown, pause or resume it, and receive an alert when it finishes.
- **Media** — view the current track, album artwork, playback state, and media controls.
- **System Monitor** — follow CPU, memory, and network activity at a glance.
- **Power** — view AC or battery state and battery percentage where available.
- **Clipboard** — browse recent clipboard entries and copy, remove, or send an entry to the focused application.
- **Drop Shelf** — temporarily collect files and folders, then drag them together to another location.
- **Focus Session** — run a focused work session with a clear remaining-time display.
- **Audio Switcher** — move quickly between available playback devices.
- **Voice Notes** — record short audio notes and revisit recent recordings.
- **Colour Picker** — sample a screen colour and copy its HEX, RGB, or HSL value.
- **Screen Capture** — capture a region, a window, one display, or all displays directly to the Captures folder.
- **Privacy Controls** — monitor the default microphone state and mute or unmute it globally.
- **Removable Devices** — view connected removable storage, inspect capacity, open it in Explorer, and request safe ejection. Multiple devices are presented as pages within the module.
- **Bluetooth Devices** — see connected Bluetooth accessories and their available battery levels. Newly connected or newly low-battery devices can automatically request attention.

## Architecture

Each feature is an independent pair of projects:

- `Glance.<Module>` contains the platform-neutral state and view-model logic.
- `Glance.<Module>.WinUI` contains the Windows integration, views, theme resources, and localized strings.

Modules implement `IGlanceModule` and are discovered recursively from the `Modules` application directory at startup. Each module is published to `Modules/<Module>` with its domain assembly, WinUI assembly, private dependencies, symbols, and PRI resources kept together. Shared application and framework dependencies, including `Glance.UI.WinUI`, remain beside `Glance.exe`, while the module loader resolves module-owned assemblies from their module directories.

The shell has no compile-time reference to the presentation assemblies of its built-in modules. At startup it loads each module's PRI, registers its generated WinUI XAML metadata provider, discovers `IGlanceModule` implementations, and then lets the module register its own services. The built-in modules therefore use the same runtime path as an independently supplied module. Unit tests are kept in matching `Glance.<Module>.Tests` projects.

Glance consumes Elysium through NuGet package references, using the shared version declared in `Directory.Build.props`.

## Third-party modules

A third-party module is installed by placing its bundle in a new directory below `%LOCALAPPDATA%\Glance\Modules` before Glance starts:

```text
%LOCALAPPDATA%/Glance/Modules/
  Example/
    Example.Glance.WinUI.dll
    Example.Glance.WinUI.pri
    Example.Glance.dll
    Example.Dependency.dll
```

Glance scans both this user-writable location and the built-in `Modules` directory beside `Glance.exe`. Both locations use the same loader. The entry assembly and PRI must have the same base filename. Glance does not require the assembly name to begin with `Glance` or to be known when the application is compiled.

The module's WinUI project must:

- Reference the matching `Glance.Application.Abstractions` contract and expose a public, parameterless `IGlanceModule` implementation.
- Set `UseWinUI` to `true`.
- Set `DisableEmbeddedXbf` to `false` so compiled XAML is embedded in the module PRI.
- Set `CopyLocalLockFileAssemblies` to `true`, or otherwise include every private runtime dependency in the module directory.
- Target x64 and a framework compatible with Glance's .NET 10 and Windows App SDK 2.2 runtime.
- Keep shared Glance, Elysium, WinUI, and Microsoft Extensions contract assemblies out of the module bundle so the host-provided versions are used.

Modules are loaded at startup and run with the same trust as Glance itself. Only install modules from sources you trust.

## Settings

Settings follow the same Elysium application and navigation structure used by [Infinity](https://github.com/elysium-studio/Infinity). They currently provide:

- Drag-and-drop module ordering
- Per-module enable and disable switches
- Top or bottom desktop-island placement
- Start Glance with Windows

## Building

Glance targets Windows with .NET 10 and WinUI 3. Open `Glance.slnx` in Visual Studio or build the x64 solution from the command line:

```powershell
dotnet build Glance.slnx -p:Platform=x64
```

The application entry point is `Glance.Shell.WinUI`.

## Releasing

Glance uses the same release model as Infinity: Velopack produces the directly distributed installer and update feed, while a separately generated MSIX upload is used for Microsoft Store releases. Store-packaged builds detect their package identity and do not initialise Velopack at runtime.

Release builds remain self-contained managed .NET applications rather than using Native AOT. Glance discovers its `Modules/<Module>/Glance.*.WinUI.dll` modules at runtime, so those directories must remain available for module loading.

Copy `publish.local.example.json` to the ignored `publish.local.json`, fill in the credentials and Store identity values, then run:

```powershell
.\publish.ps1
```

Useful alternatives include `-SkipMicrosoftStore`, `-MicrosoftStorePackageOnly`, `-MicrosoftStoreDraft`, and `-GitReleaseOnly`. The script supports Azure Trusted Signing, SFTP update-feed upload, GitHub releases, and Partner Center submission. See [`Store/README.md`](Store/README.md) for the Microsoft Store prerequisites and servicing model.
