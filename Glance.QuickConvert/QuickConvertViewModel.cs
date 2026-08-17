using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.QuickConvert;

public sealed partial class QuickConvertViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    private string summary = localizer.GetText("ReadySummary");

    [ObservableProperty]
    private string detail = localizer.GetText("ReadyDetail");

    [ObservableProperty]
    private bool isConverting;

    [ObservableProperty]
    private bool isComplete;

    [ObservableProperty]
    private int queuedJobs;

    private int currentConversionCount;

    public bool IsBusy => IsConverting || QueuedJobs > 0;

    public string Title => localizer.GetText("ModuleTitle");

    public void Prepare(int count)
    {
        if (IsBusy)
        {
            return;
        }

        IsConverting = false;
        IsComplete = false;
        Summary = localizer.GetText(count == 1 ? "OneFileReady" : "ManyFilesReady", count);
        Detail = localizer.GetText("ChooseOptions");
    }

    public void CancelPreview()
    {
        if (IsBusy)
        {
            return;
        }

        IsComplete = false;
        Summary = localizer.GetText("ReadySummary");
        Detail = localizer.GetText("ReadyDetail");
    }

    public void BeginConversion(int count)
    {
        currentConversionCount = count;
        IsConverting = true;
        IsComplete = false;
        Summary = localizer.GetText(count == 1 ? "ConvertingOneFile" : "ConvertingManyFiles", count);
        Detail = localizer.GetText("PleaseWait");
    }

    public void ShowToolSetup(double progress)
    {
        IsConverting = true;
        IsComplete = false;
        Summary = localizer.GetText("SettingUp");
        Detail = localizer.GetText("DownloadingTools", Math.Round(Math.Clamp(progress, 0, 1) * 100));
    }

    public void CompleteToolSetup() => BeginConversion(Math.Max(1, currentConversionCount));

    public void ShowToolSetupFailure()
    {
        QueuedJobs = 0;
        IsConverting = false;
        IsComplete = false;
        Summary = localizer.GetText("SetupFailed");
        Detail = localizer.GetText("SetupFailedDetail");
    }

    public void Enqueue(int jobs)
    {
        QueuedJobs = jobs;
        IsComplete = false;

        if (!IsConverting)
        {
            Summary = localizer.GetText(jobs == 1 ? "OneJobQueued" : "ManyJobsQueued", jobs);
            Detail = localizer.GetText("WaitingToConvert");
            return;
        }

        int waiting = Math.Max(0, jobs - 1);
        Detail = localizer.GetText(waiting == 1 ? "OneMoreJobQueued" : "ManyMoreJobsQueued", waiting);
    }

    public void Complete(int successful,
        int failed,
        int jobsRemaining)
    {
        QueuedJobs = jobsRemaining;

        if (jobsRemaining > 0)
        {
            IsConverting = false;
            IsComplete = false;
            Summary = localizer.GetText(jobsRemaining == 1 ? "OneJobQueued" : "ManyJobsQueued", jobsRemaining);
            Detail = localizer.GetText("NextJobStarting");
            return;
        }

        IsConverting = false;
        IsComplete = true;
        Summary = localizer.GetText(successful == 1 ? "ConvertedOneFile" : "ConvertedManyFiles", successful);
        Detail = failed == 0
            ? localizer.GetText("SavedBesideOriginals")
            : localizer.GetText("ConversionFailures", failed);
    }

    public void StopConversions()
    {
        QueuedJobs = 0;
        IsConverting = false;
        IsComplete = false;
        Summary = localizer.GetText("ReadySummary");
        Detail = localizer.GetText("ReadyDetail");
    }

    partial void OnIsConvertingChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    partial void OnQueuedJobsChanged(int value) => OnPropertyChanged(nameof(IsBusy));
}
