using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorPickerComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IColorPickerService colorPickerService;
    private readonly IGlanceAttentionService attentionService;
    private readonly ITextLocalizer localizer;
    private readonly ColorPickerViewModel viewModel;
    private readonly GlanceModuleOptions<ColorPickerSettings> options;
    private readonly DispatcherQueue dispatcherQueue;

    public ColorPickerComponent(
        ColorPickerViewModel viewModel,
        IColorPickerService colorPickerService,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<ColorPickerSettings> options,
        ModuleResourceTextLocalizer<ColorPickerModule> localizer)
    {
        this.colorPickerService = colorPickerService;
        this.attentionService = attentionService;
        this.viewModel = viewModel;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ColorPickerCompactView compactView = new(viewModel);
        ColorPickerExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        colorPickerService.ColorPicked += HandleColorPicked;
        options.Changed += HandleOptionsChanged;
    }

    public string Id => "ColorPicker";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 100;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        colorPickerService.ColorPicked -= HandleColorPicked;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ColorPickerSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

    private void HandleColorPicked(object? sender, ColorPickerEventArgs args) =>
        attentionService.RequestAttention(Id);
}
