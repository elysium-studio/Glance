using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Archive;

public sealed partial class ArchiveViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    private string summary = localizer.GetText("ReadySummary");

    [ObservableProperty]
    private string detail = localizer.GetText("ReadyDetail");

    [ObservableProperty]
    private bool isBusy;

    public string Title => localizer.GetText("ModuleTitle");

    public void Preview(bool containsOnlyArchives, int count)
    {
        if (IsBusy)
        {
            return;
        }

        Summary = localizer.GetText(containsOnlyArchives ? count == 1 ? "ExtractOneArchive" : "ExtractManyArchives" : count == 1 ? "CreateOneArchive" : "CreateManyArchives", count);
        Detail = localizer.GetText("DropToChoose");
    }

    public void CancelPreview()
    {
        if (!IsBusy)
        {
            Reset();
        }
    }

    public void Begin(ArchiveOperation operation, int count)
    {
        IsBusy = true;
        Summary = localizer.GetText(operation switch
        {
            ArchiveOperation.Extract => count == 1 ? "ExtractingOneArchive" : "ExtractingManyArchives",
            ArchiveOperation.Convert => count == 1 ? "ConvertingOneArchive" : "ConvertingManyArchives",
            _ => "CreatingArchive"
        }, count);
        Detail = localizer.GetText("PleaseWait");
    }

    public void Complete(ArchiveOperation operation, int count)
    {
        IsBusy = false;
        Summary = localizer.GetText(operation switch
        {
            ArchiveOperation.Extract => count == 1 ? "ExtractedOneArchive" : "ExtractedManyArchives",
            ArchiveOperation.Convert => count == 1 ? "ConvertedOneArchive" : "ConvertedManyArchives",
            _ => "ArchiveCreated"
        }, count);
        Detail = localizer.GetText("SavedBesideOriginals");
    }

    public void Fail(string reason)
    {
        IsBusy = false;
        Summary = localizer.GetText("ArchiveOperationFailed");
        Detail = reason;
    }

    public void Reset()
    {
        IsBusy = false;
        Summary = localizer.GetText("ReadySummary");
        Detail = localizer.GetText("ReadyDetail");
    }
}
