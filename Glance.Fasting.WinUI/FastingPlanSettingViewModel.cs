using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Fasting.WinUI;

public sealed partial class FastingPlanSettingViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    private readonly SemaphoreSlim saveSynchronization = new(1, 1);
    private readonly IWritableOptions<FastingSettings> writer;
    private int disposed;
    private int saveQueued;
    private bool initialized;

    public FastingPlanSettingViewModel(FastingSettings settings, IWritableOptions<FastingSettings> writer)
    {
        this.writer = writer;
        SelectedPlanIndex = (int)settings.Plan;
        CustomFastingHours = FastingPlanCatalog.NormalizeCustomHours(settings.CustomFastingHours);
        CustomEatingHours = FastingPlanCatalog.NormalizeCustomEatingHours(settings.CustomEatingHours);
        initialized = true;
    }

    [ObservableProperty]
    public partial double CustomEatingHours { get; set; }

    [ObservableProperty]
    public partial double CustomFastingHours { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    public partial int SelectedPlanIndex { get; set; }

    public bool IsCustom => SelectedPlanIndex == (int)FastingPlan.Custom;

    public string ModuleId => "Fasting";

    public int Order => 10;

    public void Dispose() => _ = Interlocked.Exchange(ref disposed, 1);

    partial void OnCustomFastingHoursChanged(double value) => QueueSave();

    partial void OnCustomEatingHoursChanged(double value) => QueueSave();

    partial void OnSelectedPlanIndexChanged(int value) => QueueSave();

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
                FastingPlan plan = Enum.IsDefined(typeof(FastingPlan), SelectedPlanIndex) ? (FastingPlan)SelectedPlanIndex : FastingPlan.SixteenEight;
                double customFastingHours = FastingPlanCatalog.NormalizeCustomHours(CustomFastingHours);
                double customEatingHours = FastingPlanCatalog.NormalizeCustomEatingHours(CustomEatingHours);
                await writer.WriteAsync(settings =>
                {
                    settings.Plan = plan;
                    settings.CustomFastingHours = customFastingHours;
                    settings.CustomEatingHours = customEatingHours;
                });
            }
        }
        finally
        {
            _ = saveSynchronization.Release();
        }
    }
}
