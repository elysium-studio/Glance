using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.ScreenLens;

public sealed partial class ScreenLensViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    private bool isExtracting;

    public string Title => localizer.GetText("ModuleTitle");

    public string StatusText => localizer.GetText("ReadyStatus");

    public event EventHandler? ExtractionRequested;

    public void Extract()
    {
        if (IsExtracting)
        {
            return;
        }

        IsExtracting = true;
        ExtractionRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Complete() => IsExtracting = false;
}
