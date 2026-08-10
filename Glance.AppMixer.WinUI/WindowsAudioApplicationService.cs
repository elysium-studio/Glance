using Glance.AppMixer;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Glance.AppMixer.WinUI;

public sealed class WindowsAudioApplicationService :
    IAudioApplicationService
{
    private const string SystemSoundsId = "system-sounds";
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint SnapshotProcesses = 0x00000002;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaximumProcessDepth = 16;

    public IReadOnlyList<AudioApplicationSession> GetApplications()
    {
        try
        {
            IReadOnlyDictionary<int, int> parentProcessIds = GetParentProcessIds();
            ForegroundApplication foregroundApplication = GetForegroundApplication(parentProcessIds);
            List<SessionSnapshot> snapshots = ReadSessions(parentProcessIds);

            return [.. snapshots
                .GroupBy(snapshot => snapshot.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AudioApplicationSession(group.Key,
                    group.Select(snapshot => snapshot.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? group.Key,
                    (int)Math.Round(group.Average(snapshot => snapshot.VolumePercent)),
                    group.All(snapshot => snapshot.IsMuted),
                    group.Max(snapshot => snapshot.Peak),
                    group.Any(snapshot => foregroundApplication.Contains(snapshot.ProcessId, snapshot.ProcessName)),
                    group.Any(snapshot => snapshot.IsActive)))
                .OrderByDescending(application => application.IsForeground)
                .ThenByDescending(application => application.Peak)
                .ThenByDescending(application => application.IsActive)
                .ThenBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (COMException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    public bool TrySetVolume(string applicationId,
        int volumePercent) => UpdateSessions(applicationId, session => session.SimpleAudioVolume.Volume = Math.Clamp(volumePercent, 0, 100) / 100f);

    public bool TrySetMuted(string applicationId,
        bool isMuted) => UpdateSessions(applicationId, session => session.SimpleAudioVolume.Mute = isMuted);

    private static List<SessionSnapshot> ReadSessions(IReadOnlyDictionary<int, int> parentProcessIds)
    {
        List<SessionSnapshot> snapshots = [];

        using MMDeviceEnumerator enumerator = new();
        using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        AudioSessionManager manager = device.AudioSessionManager;

        try
        {
            SessionCollection sessions = manager.Sessions;

            for (int index = 0; index < sessions.Count; index++)
            {
                using AudioSessionControl session = sessions[index];

                try
                {
                    SessionIdentity identity = GetSessionIdentity(session, parentProcessIds);

                    if (IsGlanceSession(session, identity))
                    {
                        continue;
                    }

                    snapshots.Add(new SessionSnapshot(identity.Id,
                        identity.ProcessId,
                        identity.ProcessName,
                        identity.DisplayName,
                        session.SimpleAudioVolume.Volume * 100,
                        session.SimpleAudioVolume.Mute,
                        session.AudioMeterInformation.MasterPeakValue,
                        session.State == AudioSessionState.AudioSessionStateActive));
                }
                catch (COMException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        finally
        {
            manager.Dispose();
        }

        return snapshots;
    }

    private static bool UpdateSessions(string applicationId,
        Action<AudioSessionControl> update)
    {
        bool updated = false;

        try
        {
            using MMDeviceEnumerator enumerator = new();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            AudioSessionManager manager = device.AudioSessionManager;

            try
            {
                SessionCollection sessions = manager.Sessions;
                IReadOnlyDictionary<int, int> parentProcessIds = GetParentProcessIds();

                for (int index = 0; index < sessions.Count; index++)
                {
                    using AudioSessionControl session = sessions[index];

                    try
                    {
                        SessionIdentity identity = GetSessionIdentity(session, parentProcessIds);

                        if (IsGlanceSession(session, identity))
                        {
                            continue;
                        }

                        if (!string.Equals(identity.Id, applicationId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        update(session);
                        updated = true;
                    }
                    catch (COMException)
                    {
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            finally
            {
                manager.Dispose();
            }
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return updated;
    }

    private static bool IsGlanceSession(AudioSessionControl session,
        SessionIdentity identity) => !session.IsSystemSoundsSession &&
        (session.GetProcessID == (uint)Environment.ProcessId || identity.ProcessId == Environment.ProcessId);

    private static SessionIdentity GetSessionIdentity(AudioSessionControl session,
        IReadOnlyDictionary<int, int> parentProcessIds)
    {
        if (session.IsSystemSoundsSession || session.GetProcessID == 0)
        {
            return new SessionIdentity(0, SystemSoundsId, SystemSoundsId, "System sounds");
        }

        int processId = checked((int)session.GetProcessID);

        using Process process = Process.GetProcessById(processId);
        string processName = process.ProcessName;
        bool isWebViewHost = IsWebViewHost(processName);
        ResolvedProcess resolvedProcess = isWebViewHost
            ? ResolveOwningProcess(processId, processName, parentProcessIds)
            : new ResolvedProcess(processId, processName);
        string id = isWebViewHost && resolvedProcess.ProcessId != processId
            ? TryGetPackageFamilyName(resolvedProcess.ProcessId) ?? resolvedProcess.ProcessName.ToLowerInvariant()
            : resolvedProcess.ProcessName.ToLowerInvariant();
        string displayName = GetDisplayName(session,
            resolvedProcess.ProcessId,
            resolvedProcess.ProcessName,
            isWebViewHost && resolvedProcess.ProcessId != processId);
        return new SessionIdentity(resolvedProcess.ProcessId, id, resolvedProcess.ProcessName, displayName);
    }

    private static string GetDisplayName(AudioSessionControl session,
        int processId,
        string fallback,
        bool preferProcessIdentity)
    {
        if (!preferProcessIdentity && IsUsableSessionDisplayName(session.DisplayName))
        {
            return session.DisplayName;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            string? description = process.MainModule?.FileVersionInfo.FileDescription;

            if (!string.IsNullOrWhiteSpace(description) && !IsWebViewDisplayName(description))
            {
                return description;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
        }

        return IsUsableSessionDisplayName(session.DisplayName) && !IsWebViewDisplayName(session.DisplayName)
            ? session.DisplayName
            : Humanize(fallback);
    }

    private static bool IsUsableSessionDisplayName(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName) &&
        !displayName.StartsWith('@') &&
        !IsWebViewDisplayName(displayName);

    private static bool IsWebViewDisplayName(string displayName) =>
        displayName.Contains("WebView2", StringComparison.OrdinalIgnoreCase);

    private static bool IsWebViewHost(string processName) =>
        string.Equals(processName, "msedgewebview2", StringComparison.OrdinalIgnoreCase);

    private static ResolvedProcess ResolveOwningProcess(int processId,
        string processName,
        IReadOnlyDictionary<int, int> parentProcessIds)
    {
        ResolvedProcess resolvedProcess = new(processId, processName);
        HashSet<int> visitedProcessIds = [processId];

        for (int depth = 0; depth < MaximumProcessDepth && IsWebViewHost(resolvedProcess.ProcessName); depth++)
        {
            if (!parentProcessIds.TryGetValue(resolvedProcess.ProcessId, out int parentProcessId) ||
                parentProcessId <= 0 ||
                !visitedProcessIds.Add(parentProcessId))
            {
                break;
            }

            try
            {
                using Process parentProcess = Process.GetProcessById(parentProcessId);
                resolvedProcess = new ResolvedProcess(parentProcessId, parentProcess.ProcessName);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                break;
            }
        }

        return resolvedProcess;
    }

    private static IReadOnlyDictionary<int, int> GetParentProcessIds()
    {
        Dictionary<int, int> parentProcessIds = [];
        nint snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);

        if (snapshot == new nint(-1))
        {
            return parentProcessIds;
        }

        try
        {
            ProcessEntry entry = new()
            {
                Size = checked((uint)Marshal.SizeOf<ProcessEntry>()),
                ExecutableFile = string.Empty
            };

            if (!Process32First(snapshot, ref entry))
            {
                return parentProcessIds;
            }

            do
            {
                if (entry.ProcessId > 0 && entry.ProcessId <= int.MaxValue &&
                    entry.ParentProcessId > 0 && entry.ParentProcessId <= int.MaxValue)
                {
                    parentProcessIds[checked((int)entry.ProcessId)] = checked((int)entry.ParentProcessId);
                }

                entry.Size = checked((uint)Marshal.SizeOf<ProcessEntry>());
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }

        return parentProcessIds;
    }

    private static string? TryGetPackageFamilyName(int processId)
    {
        nint processHandle = OpenProcess(ProcessQueryLimitedInformation, false, checked((uint)processId));

        if (processHandle == 0)
        {
            return null;
        }

        try
        {
            uint nameLength = 0;
            int result = GetPackageFamilyName(processHandle, ref nameLength, null);

            if (result != ErrorInsufficientBuffer || nameLength == 0)
            {
                return null;
            }

            StringBuilder packageFamilyName = new(checked((int)nameLength));
            result = GetPackageFamilyName(processHandle, ref nameLength, packageFamilyName);
            return result == 0 && packageFamilyName.Length > 0
                ? packageFamilyName.ToString()
                : null;
        }
        finally
        {
            _ = CloseHandle(processHandle);
        }
    }

    private static string Humanize(string value)
    {
        string applicationName = value.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            ? value["Microsoft.".Length..]
            : value;
        string[] words = applicationName.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);

        return words.Length == 0
            ? value
            : string.Join(" ", words.Select(HumanizeWord));
    }

    private static string HumanizeWord(string value) => value.Length == 0
        ? value
        : char.ToUpperInvariant(value[0]) + value[1..];

    private static ForegroundApplication GetForegroundApplication(IReadOnlyDictionary<int, int> parentProcessIds)
    {
        nint window = GetForegroundWindow();
        HashSet<int> processIds = [];

        if (window == 0)
        {
            return new ForegroundApplication(processIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        AddWindowProcess(window, processIds);
        EnumWindowsProc callback = (childWindow, _) =>
        {
            AddWindowProcess(childWindow, processIds);
            return true;
        };
        _ = EnumChildWindows(window, callback, 0);

        HashSet<int> resolvedProcessIds = [.. processIds];
        HashSet<string> processNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (int processId in processIds)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                string processName = process.ProcessName;
                _ = processNames.Add(processName);

                if (IsWebViewHost(processName))
                {
                    ResolvedProcess resolvedProcess = ResolveOwningProcess(processId, processName, parentProcessIds);
                    _ = resolvedProcessIds.Add(resolvedProcess.ProcessId);
                    _ = processNames.Add(resolvedProcess.ProcessName);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }

        return new ForegroundApplication(resolvedProcessIds, processNames);
    }

    private static void AddWindowProcess(nint window,
        ISet<int> processIds)
    {
        _ = GetWindowThreadProcessId(window, out uint processId);

        if (processId != 0)
        {
            _ = processIds.Add(checked((int)processId));
        }
    }

    private delegate bool EnumWindowsProc(nint window,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint parentWindow,
        EnumWindowsProc callback,
        nint parameter);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window,
        out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags,
        uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint snapshot,
        ref ProcessEntry entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint snapshot,
        ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetPackageFamilyName(nint process,
        ref uint packageFamilyNameLength,
        StringBuilder? packageFamilyName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    private sealed record SessionIdentity(int ProcessId,
        string Id,
        string ProcessName,
        string DisplayName);

    private sealed record ResolvedProcess(int ProcessId,
        string ProcessName);

    private sealed record SessionSnapshot(string Id,
        int ProcessId,
        string ProcessName,
        string DisplayName,
        double VolumePercent,
        bool IsMuted,
        double Peak,
        bool IsActive);

    private sealed record ForegroundApplication(HashSet<int> ProcessIds,
        HashSet<string> ProcessNames)
    {
        public bool Contains(int processId,
            string processName) => ProcessIds.Contains(processId) || ProcessNames.Contains(processName);
    }
}
