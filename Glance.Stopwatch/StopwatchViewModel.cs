using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Glance.Stopwatch;

public partial class StopwatchViewModel : ObservableObject
{
    private readonly System.Diagnostics.Stopwatch stopwatch = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private string elapsed = "00:00.00";

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    [RelayCommand]
    private void Toggle()
    {
        if (IsRunning)
        {
            stopwatch.Stop();
            IsRunning = false;
        }
        else
        {
            stopwatch.Start();
            IsRunning = true;
        }

        Refresh();
    }

    [RelayCommand]
    private void Reset()
    {
        stopwatch.Reset();
        IsRunning = false;
        Refresh();
    }

    public void Refresh()
    {
        TimeSpan value = stopwatch.Elapsed;
        Elapsed = value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}";
    }
}
