using Glance.AppMixer;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
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
            string? foregroundProcessName = GetForegroundProcessName();
            List<SessionSnapshot> snapshots = ReadSessions();

            return [.. snapshots
                .GroupBy(snapshot => snapshot.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AudioApplicationSession(group.Key,
                    group.Select(snapshot => snapshot.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? group.Key,
                    (int)Math.Round(group.Average(snapshot => snapshot.VolumePercent)),
                    group.All(snapshot => snapshot.IsMuted),
                    group.Max(snapshot => snapshot.Peak),
                    foregroundProcessName is not null && group.Any(snapshot => string.Equals(snapshot.ProcessName, foregroundProcessName, StringComparison.OrdinalIgnoreCase)),
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
                    SessionIdentity identity = GetSessionIdentity(session);
                    snapshots.Add(new SessionSnapshot(identity.Id,
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

    private static SessionIdentity GetSessionIdentity(AudioSessionControl session)
    {
        if (session.IsSystemSoundsSession || session.GetProcessID == 0)
        {
            return new SessionIdentity(SystemSoundsId, SystemSoundsId, "System sounds");
        }

        int processId = checked((int)session.GetProcessID);

        using Process process = Process.GetProcessById(processId);
        string processName = process.ProcessName;
        string id = processName.ToLowerInvariant();
        string displayName = GetDisplayName(session, process, processName);
        return new SessionIdentity(id, processName, displayName);
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

    private static string Humanize(string value) => value.Length == 0
        ? value
        : char.ToUpperInvariant(value[0]) + value[1..];

    private static string? GetForegroundProcessName()
    {
        nint window = GetForegroundWindow();

        if (window == 0)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(window, out uint processId);

        if (processId == 0)
        {
            return null;
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window,
        out uint processId);

    private sealed record SessionIdentity(string Id,
        string ProcessName,
        string DisplayName);

    private sealed record SessionSnapshot(string Id,
        string ProcessName,
        string DisplayName,
        double VolumePercent,
        bool IsMuted,
        double Peak,
        bool IsActive);
}
