using Glance.Application.Abstractions;

namespace Glance.Stash.Tests;

public sealed class StashViewModelTests
{
    [Fact]
    public void Constructor_UsesLocalizedEmptyState()
    {
        StashViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.HasItems);
        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.Equal("EmptySummary", viewModel.CompactText);
    }

    [Fact]
    public void Add_StoresTextAndUpdatesCompactState()
    {
        StashViewModel viewModel = CreateViewModel();

        StashItem? item = viewModel.Add("A useful note", false);

        Assert.NotNull(item);
        Assert.True(viewModel.HasItems);
        Assert.Equal(StashItemKind.Text, item.Kind);
        Assert.Equal("A useful note", viewModel.CompactText);
        Assert.Same(item, viewModel.SelectedItem);
    }

    [Fact]
    public void Add_DetectsWebLinksFromPlainText()
    {
        StashViewModel viewModel = CreateViewModel();

        StashItem? item = viewModel.Add("https://github.com/elysium-studio/Glance", false);

        Assert.NotNull(item);
        Assert.Equal(StashItemKind.Link, item.Kind);
        Assert.True(item.CanOpen);
        Assert.Equal("github.com/elysium-studio/Glance", item.DisplayText);
    }

    [Fact]
    public void Add_OffersEditorOnlyForMultilineText()
    {
        StashViewModel viewModel = CreateViewModel();

        StashItem multiline = viewModel.Add("First line\r\nSecond line", false)!;
        StashItem singleLine = viewModel.Add("One line", false)!;
        StashItem link = viewModel.Add("https://github.com", true)!;

        Assert.True(multiline.CanOpenInEditor);
        Assert.False(singleLine.CanOpenInEditor);
        Assert.False(link.CanOpenInEditor);
    }

    [Fact]
    public void Add_PromotesDuplicateWithoutDuplicatingIt()
    {
        StashViewModel viewModel = CreateViewModel();
        StashItem first = viewModel.Add("First", false)!;
        viewModel.Add("Second", false);

        StashItem? promoted = viewModel.Add("First", false);

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Same(promoted, viewModel.Items[0]);
        Assert.Equal(first.Id, promoted!.Id);
    }

    [Fact]
    public void Restore_OrdersNewestFirstAndIgnoresInvalidEntries()
    {
        StashViewModel viewModel = CreateViewModel();
        DateTimeOffset now = DateTimeOffset.Now;

        viewModel.Restore([
            new StashEntry("older", StashItemKind.Text, "Older", now.AddMinutes(-1)),
            new StashEntry("", StashItemKind.Text, "Invalid", now),
            new StashEntry("newer", StashItemKind.Text, "Newer", now)]);

        Assert.Equal(["Newer", "Older"], viewModel.Items.Select(item => item.Content));
        Assert.Equal("Newer", viewModel.CompactText);
    }

    [Fact]
    public async Task Remove_UpdatesSelectionAndEmptyState()
    {
        StashViewModel viewModel = CreateViewModel();
        StashItem item = viewModel.Add("Note", false)!;
        viewModel.ConfigureActions(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);

        await viewModel.RemoveAsync(item);

        Assert.False(viewModel.HasItems);
        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.Equal("EmptySummary", viewModel.CompactText);
    }

    [Fact]
    public void UpdateContent_ReplacesTheItemAndPreservesItsIdentity()
    {
        StashViewModel viewModel = CreateViewModel();
        StashItem original = viewModel.Add("First line\r\nSecond line", false)!;

        StashItem? updated = viewModel.UpdateContent(original.Id, "Edited first line\r\nSecond line");

        Assert.NotNull(updated);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal("Edited first line\r\nSecond line", updated.Content);
        Assert.Same(updated, viewModel.SelectedItem);
        Assert.Equal("Edited first line Second line", viewModel.CompactText);
    }

    private static StashViewModel CreateViewModel() => new(new TestTextLocalizer());

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key,
            params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
