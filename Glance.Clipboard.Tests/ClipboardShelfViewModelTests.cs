using Glance.Application.Abstractions;

namespace Glance.Clipboard.Tests;

public sealed class ClipboardShelfViewModelTests
{
    private readonly TestTextLocalizer localizer = new();

    [Fact]
    public void Constructor_UsesLocalizedEmptyState()
    {
        ClipboardShelfViewModel viewModel = new(localizer);

        Assert.Equal("ModuleTitle", viewModel.Title);
        Assert.Equal("ClipboardEmpty", viewModel.LatestPreview);
        Assert.Equal("CopySomethingToBegin", viewModel.LatestKind);
        Assert.Equal("WaitingForClipboard", viewModel.HistoryStatus);
        Assert.False(viewModel.CanClearHistory);
        Assert.False(viewModel.CanUseSelectedEntry);
    }

    [Fact]
    public void Update_WithEntries_SelectsNewestAndPopulatesSummary()
    {
        ClipboardShelfViewModel viewModel = new(localizer);
        ClipboardEntry newest = CreateEntry("1", "Newest", "Text", "A");
        ClipboardEntry older = CreateEntry("2", "Older", "Image", "B");

        viewModel.Update([newest, older], "Ready");

        Assert.Equal("Newest", viewModel.LatestPreview);
        Assert.Equal("Text", viewModel.LatestKind);
        Assert.Equal("A", viewModel.LatestGlyph);
        Assert.Equal("Ready", viewModel.HistoryStatus);
        Assert.True(viewModel.CanClearHistory);
        Assert.True(viewModel.CanUseSelectedEntry);
        Assert.Same(newest, viewModel.SelectedEntry);
        Assert.Equal([newest, older], viewModel.ShelfItems);
    }

    [Fact]
    public void Update_LimitsShelfToSixEntries()
    {
        ClipboardShelfViewModel viewModel = new(localizer);
        ClipboardEntry[] entries = Enumerable.Range(1, 8)
            .Select(index => CreateEntry(index.ToString(), $"Item {index}"))
            .ToArray();

        viewModel.Update(entries, "Ready");

        Assert.Equal(6, viewModel.ShelfItems.Count);
        Assert.Equal(entries.Take(6), viewModel.ShelfItems);
    }

    [Fact]
    public void Update_WithNoEntries_RestoresEmptyState()
    {
        ClipboardShelfViewModel viewModel = new(localizer);
        viewModel.Update([CreateEntry("1", "Value")], "Ready");

        viewModel.Update([], "Empty");

        Assert.Empty(viewModel.ShelfItems);
        Assert.Null(viewModel.SelectedEntry);
        Assert.Equal("ClipboardEmpty", viewModel.LatestPreview);
        Assert.Equal("CopySomethingToBegin", viewModel.LatestKind);
        Assert.False(viewModel.CanClearHistory);
        Assert.False(viewModel.CanUseSelectedEntry);
    }

    [Theory]
    [InlineData(true, "CopiedToClipboard")]
    [InlineData(false, "CopyFailed")]
    public async Task CopyAsync_ReportsActionResult(bool result, string expectedStatus)
    {
        ClipboardShelfViewModel viewModel = CreateConfiguredViewModel(copyResult: result);

        await viewModel.CopyAsync(CreateEntry("1", "Value"));

        Assert.Equal(expectedStatus, viewModel.HistoryStatus);
    }

    [Theory]
    [InlineData(true, "SentToFocusedApp")]
    [InlineData(false, "SendFailed")]
    public async Task PasteAsync_ReportsActionResult(bool result, string expectedStatus)
    {
        ClipboardShelfViewModel viewModel = CreateConfiguredViewModel(pasteResult: result);

        await viewModel.PasteAsync(CreateEntry("1", "Value"));

        Assert.Equal(expectedStatus, viewModel.HistoryStatus);
    }

    [Theory]
    [InlineData(true, "RemovedFromHistory")]
    [InlineData(false, "RemoveFailed")]
    public async Task RemoveAsync_ReportsActionResult(bool result, string expectedStatus)
    {
        ClipboardShelfViewModel viewModel = CreateConfiguredViewModel(removeResult: result);

        await viewModel.RemoveAsync(CreateEntry("1", "Value"));

        Assert.Equal(expectedStatus, viewModel.HistoryStatus);
    }

    [Theory]
    [InlineData(true, "HistoryCleared")]
    [InlineData(false, "ClearFailed")]
    public async Task ClearAsync_ReportsActionResult(bool result, string expectedStatus)
    {
        ClipboardShelfViewModel viewModel = CreateConfiguredViewModel(clearResult: result);

        await viewModel.ClearAsync();

        Assert.Equal(expectedStatus, viewModel.HistoryStatus);
    }

    [Fact]
    public async Task SelectedActions_UseCurrentSelection()
    {
        List<(string Action, ClipboardEntry Entry)> calls = [];
        ClipboardShelfViewModel viewModel = new(localizer);
        viewModel.ConfigureActions(
            entry => Record("Copy", entry),
            entry => Record("Paste", entry),
            entry => Record("Remove", entry),
            () => Task.FromResult(true));
        ClipboardEntry selected = CreateEntry("selected", "Value");
        viewModel.SelectedEntry = selected;

        await viewModel.CopySelectedAsync();
        await viewModel.PasteSelectedAsync();
        await viewModel.RemoveSelectedAsync();

        Assert.Equal(["Copy", "Paste", "Remove"], calls.Select(call => call.Action));
        Assert.All(calls, call => Assert.Same(selected, call.Entry));
        return;

        Task<bool> Record(string action, ClipboardEntry entry)
        {
            calls.Add((action, entry));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task UnconfiguredActions_LeaveStatusUnchanged()
    {
        ClipboardShelfViewModel viewModel = new(localizer);
        string original = viewModel.HistoryStatus;
        ClipboardEntry entry = CreateEntry("1", "Value");

        await viewModel.CopyAsync(entry);
        await viewModel.PasteAsync(entry);
        await viewModel.RemoveAsync(entry);
        await viewModel.ClearAsync();

        Assert.Equal(original, viewModel.HistoryStatus);
    }

    [Theory]
    [InlineData(0, "TimeNow")]
    [InlineData(2, "2m")]
    [InlineData(120, "2h")]
    public void ClipboardEntry_TimeText_DescribesAge(int minutesOld, string expected)
    {
        ClipboardEntry entry = CreateEntry(
            "1",
            "Value",
            timestamp: DateTimeOffset.Now.AddMinutes(-minutesOld));

        Assert.Equal(expected, entry.TimeText);
    }

    private ClipboardShelfViewModel CreateConfiguredViewModel(
        bool copyResult = true,
        bool pasteResult = true,
        bool removeResult = true,
        bool clearResult = true)
    {
        ClipboardShelfViewModel viewModel = new(localizer);
        viewModel.ConfigureActions(
            _ => Task.FromResult(copyResult),
            _ => Task.FromResult(pasteResult),
            _ => Task.FromResult(removeResult),
            () => Task.FromResult(clearResult));
        return viewModel;
    }

    private ClipboardEntry CreateEntry(
        string id,
        string preview,
        string kind = "Text",
        string glyph = "G",
        DateTimeOffset? timestamp = null) =>
        new(id, preview, kind, glyph, timestamp ?? DateTimeOffset.Now, localizer);

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
    }
}
