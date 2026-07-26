using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.ColorPicker.WinUI;

public sealed partial class ColorPickerComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ITextLocalizer localizer;
    private readonly ColorPickerViewModel viewModel;
    private readonly GlanceModuleOptions<ColorPickerSettings> options;
    private readonly DispatcherQueue dispatcherQueue;

    public ColorPickerComponent(ColorPickerViewModel viewModel,
        GlanceModuleOptions<ColorPickerSettings> options,
        ModuleResourceTextLocalizer<ColorPickerModule> localizer)
    {
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
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ColorPickerSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));
}
