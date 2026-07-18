using Glance.Application.Abstractions;
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
    private readonly PowerViewModel viewModel;
    private readonly IGlanceAttentionService attentionService;
    private int attentionBand;

    public PowerComponent(
        PowerViewModel viewModel,
        IGlanceAttentionService attentionService)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
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

        PowerSnapshot initialSnapshot = PowerStateReader.Read();
        attentionBand = GetAttentionBand(initialSnapshot);
        viewModel.Update(initialSnapshot);
    }

    public string Id => "Power";

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
    }

    private void HandlePowerChanged(object? sender, object args) =>
        dispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        PowerSnapshot snapshot = PowerStateReader.Read();
        int currentBand = GetAttentionBand(snapshot);

        viewModel.Update(snapshot);

        if (currentBand > attentionBand)
        {
            attentionService.RequestAttention(
                Id,
                currentBand >= 2
                    ? GlanceAttentionLevel.Critical
                    : GlanceAttentionLevel.Default);
        }

        attentionBand = currentBand;
    }

    private static int GetAttentionBand(PowerSnapshot snapshot)
    {
        if (snapshot.BatteryState != BatteryState.Discharging)
        {
            return 0;
        }

        return snapshot.ChargePercent switch
        {
            <= 10 => 2,
            <= 20 => 1,
            _ => 0
        };
    }
}
