using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.QuickConvert.OnlineMedia;

internal sealed class OnlineMediaQuickConverterEditor :
    IGlanceQuickConverterEditor
{
    private readonly ComboBox formatPicker;
    private readonly ModuleResourceTextLocalizer<OnlineMediaQuickConverterModule> localizer;
    private readonly ComboBox qualityPicker;

    public OnlineMediaQuickConverterEditor(GlanceContentContext context,
        ModuleResourceTextLocalizer<OnlineMediaQuickConverterModule> localizer)
    {
        this.localizer = localizer;
        formatPicker = new ComboBox
        {
            Header = localizer.GetText("OutputFormat"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "MP4", "MKV", "WebM", "MP3", "M4A", "Opus", "FLAC", "WAV" },
            SelectedIndex = 0
        };
        qualityPicker = new ComboBox
        {
            Header = localizer.GetText("DownloadQuality"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                localizer.GetText("BestAvailable"),
                "1080p",
                "720p",
                "480p"
            },
            SelectedIndex = 0
        };
        TextBlock source = new()
        {
            Text = context.Content,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"] as Style
        };
        ToolTipService.SetToolTip(source, context.Content);
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(source);
        content.Children.Add(formatPicker);
        content.Children.Add(qualityPicker);
        Content = content;
        formatPicker.SelectionChanged += HandleFormatChanged;
    }

    public object Content { get; }

    public bool TryCreateOptions(out object? options,
        out string? errorMessage)
    {
        string format = formatPicker.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mp4";
        int maximumHeight = qualityPicker.SelectedIndex switch
        {
            1 => 1080,
            2 => 720,
            3 => 480,
            _ => 0
        };
        string destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "Glance Downloads");
        options = new YtDlpConversionOptions(format, maximumHeight, destination);
        errorMessage = null;
        return true;
    }

    private void HandleFormatChanged(object sender,
        SelectionChangedEventArgs args) => qualityPicker.Visibility = formatPicker.SelectedIndex < 3
            ? Visibility.Visible
            : Visibility.Collapsed;
}
