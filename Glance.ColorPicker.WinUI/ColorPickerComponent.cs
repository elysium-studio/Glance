using Glance.Application.Abstractions;
using Glance.UI.WinUI;
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

    public ColorPickerComponent(
        ColorPickerViewModel viewModel,
        IColorPickerService colorPickerService,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<ColorPickerModule> localizer)
    {
        this.colorPickerService = colorPickerService;
        this.attentionService = attentionService;
        this.localizer = localizer;

        ColorPickerCompactView compactView = new(viewModel);
        ColorPickerExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        colorPickerService.ColorPicked += HandleColorPicked;
    }

    public string Id => "ColorPicker";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 100;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose() =>
        colorPickerService.ColorPicked -= HandleColorPicked;

    private void HandleColorPicked(object? sender, ColorPickerEventArgs args) =>
        attentionService.RequestAttention(Id);
}
