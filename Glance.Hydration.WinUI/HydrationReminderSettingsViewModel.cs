using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationReminderSettingsViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    private readonly SemaphoreSlim saveSynchronization = new(1, 1);
    private readonly IWritableOptions<HydrationSettings> writer;
    private int disposed;
    private int saveQueued;
    private bool initialized;

    public HydrationReminderSettingsViewModel(HydrationSettings settings, IWritableOptions<HydrationSettings> writer)
    {
        this.writer = writer;
        RemindersEnabled = settings.RemindersEnabled;
        ReminderIntervalMinutes = HydrationSettings.NormalizeReminderInterval(settings.ReminderIntervalMinutes);
        ReminderStart = HydrationSettings.NormalizeReminderStart(settings.ReminderStart, settings.ReminderEnd);
        ReminderEnd = HydrationSettings.NormalizeReminderEnd(settings.ReminderEnd, ReminderStart);
        OnlyRemindWhenBehind = settings.OnlyRemindWhenBehind;
        initialized = true;
    }

    [ObservableProperty]
    public partial bool OnlyRemindWhenBehind { get; set; }

    [ObservableProperty]
    public partial TimeSpan ReminderEnd { get; set; }

    [ObservableProperty]
    public partial double ReminderIntervalMinutes { get; set; }

    [ObservableProperty]
    public partial TimeSpan ReminderStart { get; set; }

    [ObservableProperty]
    public partial bool RemindersEnabled { get; set; }

    public string ModuleId => "Hydration";

    public int Order => 30;

    public void Dispose() => _ = Interlocked.Exchange(ref disposed, 1);

    partial void OnOnlyRemindWhenBehindChanged(bool value) => QueueSave();

    partial void OnReminderEndChanged(TimeSpan value)
    {
        if (value <= ReminderStart)
        {
            ReminderEnd = HydrationSettings.NormalizeReminderEnd(ReminderStart + TimeSpan.FromHours(1), ReminderStart);
            return;
        }

        QueueSave();
    }

    partial void OnReminderIntervalMinutesChanged(double value) => QueueSave();

    partial void OnReminderStartChanged(TimeSpan value)
    {
        if (value >= ReminderEnd)
        {
            ReminderStart = HydrationSettings.NormalizeReminderStart(ReminderEnd - TimeSpan.FromHours(1), ReminderEnd);
            return;
        }

        QueueSave();
    }

    partial void OnRemindersEnabledChanged(bool value) => QueueSave();

    private void QueueSave()
    {
        if (!initialized || Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        _ = Interlocked.Exchange(ref saveQueued, 1);
        _ = SaveQueuedAsync();
    }

    private async Task SaveQueuedAsync()
    {
        await saveSynchronization.WaitAsync();

        try
        {
            while (Interlocked.Exchange(ref saveQueued, 0) != 0 && Volatile.Read(ref disposed) == 0)
            {
                bool remindersEnabled = RemindersEnabled;
                double interval = HydrationSettings.NormalizeReminderInterval(ReminderIntervalMinutes);
                TimeSpan start = HydrationSettings.NormalizeReminderStart(ReminderStart, ReminderEnd);
                TimeSpan end = HydrationSettings.NormalizeReminderEnd(ReminderEnd, start);
                bool onlyWhenBehind = OnlyRemindWhenBehind;
                await writer.WriteAsync(settings =>
                {
                    settings.RemindersEnabled = remindersEnabled;
                    settings.ReminderIntervalMinutes = interval;
                    settings.ReminderStart = start;
                    settings.ReminderEnd = end;
                    settings.OnlyRemindWhenBehind = onlyWhenBehind;
                });
            }
        }
        finally
        {
            _ = saveSynchronization.Release();
        }
    }
}
