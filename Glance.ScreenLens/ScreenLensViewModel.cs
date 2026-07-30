using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.ScreenLens;

public sealed partial class ScreenLensViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(HasText))]
    private string extractedText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private string statusText = localizer.GetText("ReadyStatus");

    [ObservableProperty]
    private string detailText = localizer.GetText("ReadyDetail");

    [ObservableProperty]
    private bool isExtracting;

    public string Title => localizer.GetText("ModuleTitle");

    public string CompactStatusText => HasText
        ? ExtractedText
        : StatusText;

    public bool HasText => !string.IsNullOrWhiteSpace(ExtractedText);

    public event EventHandler? ExtractionRequested;

    public event EventHandler? CopyRequested;

    public void Extract()
    {
        if (IsExtracting)
        {
            return;
        }

        IsExtracting = true;
        StatusText = localizer.GetText("SelectingStatus");
        DetailText = localizer.GetText("SelectingDetail");
        ExtractionRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Copy()
    {
        if (HasText)
        {
            CopyRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Complete(ScreenLensResult result)
    {
        ExtractedText = result.Text;
        StatusText = localizer.GetText("TextFoundStatus");
        DetailText = localizer.GetText("LineCountDetail", result.LineCount);
        IsExtracting = false;
    }

    public void Cancel()
    {
        StatusText = HasText
            ? localizer.GetText("TextFoundStatus")
            : localizer.GetText("ReadyStatus");
        DetailText = HasText
            ? DetailText
            : localizer.GetText("ReadyDetail");
        IsExtracting = false;
    }

    public void Fail()
    {
        StatusText = localizer.GetText("UnavailableStatus");
        DetailText = localizer.GetText("UnavailableDetail");
        IsExtracting = false;
    }
}
