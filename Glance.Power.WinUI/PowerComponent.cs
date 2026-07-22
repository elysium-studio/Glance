using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.System.Power;
using System;

namespace Glance.Power.WinUI;

public sealed class PowerComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly PowerViewModel viewModel;
    private readonly IGlanceAttentionService attentionService;
    private readonly GlanceModuleOptions<PowerSettings> options;
    private int attentionBand;

    public PowerComponent(
        PowerViewModel viewModel,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<PowerSettings> options,
        ModuleResourceTextLocalizer<PowerModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        PowerCompactView compactView = new(viewModel);
        PowerExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        PowerManager.BatteryStatusChanged += HandlePowerChanged;
        PowerManager.PowerSourceKindChanged += HandlePowerChanged;
        PowerManager.RemainingChargePercentChanged += HandlePowerChanged;
        PowerManager.RemainingDischargeTimeChanged += HandlePowerChanged;
        options.Changed += HandleOptionsChanged;

        PowerSnapshot initialSnapshot = PowerStateReader.Read();
        attentionBand = GetAttentionBand(initialSnapshot);
        viewModel.Update(initialSnapshot);
    }

    public string Id => "Power";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 40;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= HandlePowerChanged;
        PowerManager.PowerSourceKindChanged -= HandlePowerChanged;
        PowerManager.RemainingChargePercentChanged -= HandlePowerChanged;
        PowerManager.RemainingDischargeTimeChanged -= HandlePowerChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandlePowerChanged(object? sender, object args) =>
        dispatcherQueue.TryEnqueue(Refresh);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<PowerSettings> args) =>
        dispatcherQueue.TryEnqueue(() => attentionBand = GetAttentionBand(PowerStateReader.Read()));

    private void Refresh()
    {
        PowerSnapshot snapshot = PowerStateReader.Read();
        int currentBand = GetAttentionBand(snapshot);

        viewModel.Update(snapshot);

        if (currentBand > attentionBand)
        {
            attentionService.RequestAttention(Id, currentBand >= 2 ? GlanceAttentionLevel.Critical : GlanceAttentionLevel.Default);
        }

        attentionBand = currentBand;
    }

    private int GetAttentionBand(PowerSnapshot snapshot)
    {
        if (snapshot.BatteryState != BatteryState.Discharging)
        {
            return 0;
        }

        int criticalThreshold = (int)Math.Clamp(options.Current.CriticalBatteryThreshold, 5, 20);
        int lowThreshold = Math.Max(criticalThreshold, (int)Math.Clamp(options.Current.LowBatteryThreshold, 10, 50));

        return snapshot.ChargePercent <= criticalThreshold ? 2 : snapshot.ChargePercent <= lowThreshold ? 1 : 0;
    }
}
