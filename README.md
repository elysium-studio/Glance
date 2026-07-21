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

## Architecture

Each feature is an independent pair of projects:

- `Glance.<Module>` contains the platform-neutral state and view-model logic.
- `Glance.<Module>.WinUI` contains the Windows integration, views, theme resources, and localized strings.

Modules implement `IGlanceModule` and are discovered from `Glance.*.WinUI.dll` assemblies at startup. This keeps the shell small and allows a module to own its dependencies and presentation. Unit tests are kept in matching `Glance.<Module>.Tests` projects.

Glance consumes Elysium through NuGet package references, using the shared version declared in `Directory.Build.props`.

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
