using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Inspector;

public sealed partial class InspectorViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    public partial string Summary { get; set; } = localizer.GetText("ReadySummary");

    [ObservableProperty]
    public partial string Detail { get; set; } = localizer.GetText("ReadyDetail");

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string Title => localizer.GetText("ModuleTitle");

    public void Preview(string subject)
    {
        if (!IsBusy)
        {
            Summary = subject;
            Detail = localizer.GetText("DropDetail");
        }
    }

    public void Begin(string subject)
    {
        IsBusy = true;
        Summary = localizer.GetText("InspectingSummary");
        Detail = subject;
    }

    public void Complete(string subject, int propertyCount, int providerCount)
    {
        IsBusy = false;
        Summary = subject;
        Detail = string.Format(localizer.GetText("InspectionCompleteDetail"), propertyCount, providerCount);
    }

    public void Cancel()
    {
        IsBusy = false;
        Summary = localizer.GetText("ReadySummary");
        Detail = localizer.GetText("ReadyDetail");
    }

    public void Fail(string subject)
    {
        IsBusy = false;
        Summary = localizer.GetText("InspectionFailedSummary");
        Detail = subject;
    }
}
