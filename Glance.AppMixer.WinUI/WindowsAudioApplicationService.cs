using Glance.AppMixer;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Glance.AppMixer.WinUI;

public sealed class WindowsAudioApplicationService :
    IAudioApplicationService
{
    private const string SystemSoundsId = "system-sounds";

    public IReadOnlyList<AudioApplicationSession> GetApplications()
    {
        try
        {
            ForegroundApplication foregroundApplication = GetForegroundApplication();
            List<SessionSnapshot> snapshots = ReadSessions();

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

    private static List<SessionSnapshot> ReadSessions()
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
                    if (IsGlanceSession(session))
                    {
                        continue;
                    }

                    SessionIdentity identity = GetSessionIdentity(session);
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

                for (int index = 0; index < sessions.Count; index++)
                {
                    using AudioSessionControl session = sessions[index];

                    try
                    {
                        if (IsGlanceSession(session))
                        {
                            continue;
                        }

                        if (!string.Equals(GetSessionIdentity(session).Id, applicationId, StringComparison.OrdinalIgnoreCase))
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

    private static bool IsGlanceSession(AudioSessionControl session) =>
        !session.IsSystemSoundsSession && session.GetProcessID == (uint)Environment.ProcessId;

    private static SessionIdentity GetSessionIdentity(AudioSessionControl session)
    {
        if (session.IsSystemSoundsSession || session.GetProcessID == 0)
        {
            return new SessionIdentity(0, SystemSoundsId, SystemSoundsId, "System sounds");
        }

        int processId = checked((int)session.GetProcessID);

        using Process process = Process.GetProcessById(processId);
        string processName = process.ProcessName;
        string id = processName.ToLowerInvariant();
        string displayName = GetDisplayName(session, process, processName);
        return new SessionIdentity(processId, id, processName, displayName);
    }

    private static string GetDisplayName(AudioSessionControl session,
        Process process,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(session.DisplayName) && !session.DisplayName.StartsWith('@'))
        {
            return session.DisplayName;
        }

        try
        {
            string? description = process.MainModule?.FileVersionInfo.FileDescription;
            return string.IsNullOrWhiteSpace(description) ? Humanize(fallback) : description;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return Humanize(fallback);
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

    private static ForegroundApplication GetForegroundApplication()
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

        HashSet<string> processNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (int processId in processIds)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                _ = processNames.Add(process.ProcessName);
            }
            catch (ArgumentException)
            {
            }
        }

        return new ForegroundApplication(processIds, processNames);
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

    private sealed record SessionIdentity(int ProcessId,
        string Id,
        string ProcessName,
        string DisplayName);

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
