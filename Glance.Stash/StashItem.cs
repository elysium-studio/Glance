using Glance.Application.Abstractions;

namespace Glance.Stash;

public sealed class StashItem(StashEntry entry,
    ITextLocalizer localizer,
    Action<StashItem> copy,
    Action<StashItem> open,
    Action<StashItem> edit,
    Action<StashItem> remove)
{
    public string Id => entry.Id;

    public StashItemKind Kind => entry.Kind;

    public string Content => entry.Content;

    public DateTimeOffset CreatedAt => entry.CreatedAt;

    public string DisplayText => GetDisplayText(entry);

    public string Detail => entry.Kind == StashItemKind.Link
        ? GetLinkDetail(entry.Content, localizer)
        : localizer.GetText("KindText");

    public string Glyph => entry.Kind == StashItemKind.Link ? "\uE71B" : "\uE8A5";

    public bool CanOpen => entry.Kind == StashItemKind.Link;

    public bool CanOpenInEditor =>
        entry.Kind == StashItemKind.Text &&
        entry.Content.IndexOfAny(['\r', '\n']) >= 0;

    public void Copy() => copy(this);

    public void Open() => open(this);

    public void OpenInEditor() => edit(this);

    public void Remove() => remove(this);

    public StashEntry ToEntry() => entry;

    private static string GetDisplayText(StashEntry entry)
    {
        string value = Normalize(entry.Content);

        if (entry.Kind == StashItemKind.Link &&
            Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            string path = uri.PathAndQuery == "/" ? string.Empty : uri.PathAndQuery;
            value = $"{uri.Host}{path}";
        }

        return value.Length <= 120 ? value : $"{value[..117]}...";
    }

    private static string GetLinkDetail(string content,
        ITextLocalizer localizer) =>
        Uri.TryCreate(content, UriKind.Absolute, out Uri? uri)
            ? localizer.GetText("LinkDetail", uri.Host)
            : localizer.GetText("KindLink");

    private static string Normalize(string value) =>
        string.Join(" ", value.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
