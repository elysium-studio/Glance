using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ColorPicker.WinUI;

public sealed partial class ColorPickerComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private readonly IColorPickerService colorPickerService;
    private readonly IGlanceAttentionService attentionService;
    private readonly ITextLocalizer localizer;
    private readonly ColorPickerViewModel viewModel;
    private readonly GlanceModuleOptions<ColorPickerSettings> options;
    private readonly DispatcherQueue dispatcherQueue;

    public ColorPickerComponent(ColorPickerViewModel viewModel,
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

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 100;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("ColorPicker.Start", Id, "Pick a colour", "Start the on-screen eyedropper to sample a colour beneath the pointer.")
        {
            SemanticTags = ["colour", "color", "picker", "eyedropper", "dropper", "sample", "pixel", "hex", "rgb", "hsl"],
            ExampleUtterances = ["pick a colour from my screen", "start the eyedropper", "tell me the colour of this pixel"]
        },
        new GlanceActionDescriptor("ColorPicker.Cancel", Id, "Cancel colour picker", "Cancel the active on-screen colour selection.")
        {
            SemanticTags = ["colour", "color", "picker", "eyedropper", "cancel", "stop"],
            ExampleUtterances = ["cancel the colour picker", "stop picking a colour", "close the eyedropper"]
        }
    ];

    public bool IsAvailable(string actionId) =>
        actionId switch
        {
            "ColorPicker.Start" => !viewModel.IsPicking,
            "ColorPicker.Cancel" => viewModel.IsPicking,
            _ => false
        };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId is not ("ColorPicker.Start" or "ColorPicker.Cancel"))
        {
            return Task.FromResult(GlanceActionResult.Unavailable());
        }

        viewModel.Pick();
        return Task.FromResult(GlanceActionResult.Success());
    }

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
