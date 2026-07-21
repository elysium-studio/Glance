using Glance.Application.Abstractions;

namespace Glance.DropShelf.Tests;

public sealed class DropShelfViewModelTests
{
    [Fact]
    public void Constructor_UsesLocalizedEmptyState()
    {
        DropShelfViewModel viewModel = CreateViewModel();

        Assert.Equal("ModuleTitle", viewModel.Title);
        Assert.Equal("DragCaption", viewModel.DragCaption);
        Assert.Equal("EmptySummary", viewModel.Summary);
        Assert.Equal("EmptyDetail", viewModel.Detail);
        Assert.False(viewModel.HasItems);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public void AddItems_StagesFilesAndFolders()
    {
        DropShelfViewModel viewModel = CreateViewModel();
        DropShelfItem file = new("C:\\Work\\notes.txt", "notes.txt", false);
        DropShelfItem folder = new("C:\\Work\\Images", "Images", true);

        viewModel.AddItems([file, folder]);

        Assert.Equal([file, folder], viewModel.Items);
        Assert.True(viewModel.HasItems);
        Assert.Equal("ManyItemsSummary(2)", viewModel.Summary);
        Assert.Equal("ReadyDetail", viewModel.Detail);
    }

    [Fact]
    public void AddItems_DeduplicatesPathsIgnoringCase()
    {
        DropShelfViewModel viewModel = CreateViewModel();

        viewModel.AddItems([
            new("C:\\Work\\notes.txt", "notes.txt", false),
            new("c:\\work\\NOTES.TXT", "NOTES.TXT", false)        ]);

        Assert.Single(viewModel.Items);
        Assert.Equal("OneItemSummary", viewModel.Summary);
    }

    [Fact]
    public void AddItems_IgnoresItemsWithoutPaths()
    {
        DropShelfViewModel viewModel = CreateViewModel();

        viewModel.AddItems([new("", "Virtual item", false)]);

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
    }

    [Fact]
    public void AddItems_PreservesPreviouslyStagedItems()
    {
        DropShelfViewModel viewModel = CreateViewModel();
        DropShelfItem first = new("C:\\One.txt", "One.txt", false);
        DropShelfItem second = new("C:\\Two.txt", "Two.txt", false);

        viewModel.AddItems([first]);
        viewModel.AddItems([second]);

        Assert.Equal([first, second], viewModel.Items);
    }

    [Fact]
    public void Remove_UpdatesSummaryAndEmptyState()
    {
        DropShelfViewModel viewModel = CreateViewModel();
        DropShelfItem item = new("C:\\One.txt", "One.txt", false);
        viewModel.AddItems([item]);

        viewModel.Remove(item);

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
        Assert.Equal("EmptySummary", viewModel.Summary);
        Assert.Equal("EmptyDetail", viewModel.Detail);
    }

    [Fact]
    public void Clear_RemovesEveryItem()
    {
        DropShelfViewModel viewModel = CreateViewModel();
        viewModel.AddItems([
            new("C:\\One.txt", "One.txt", false),
            new("C:\\Two.txt", "Two.txt", false)        ]);

        viewModel.Clear();

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
    }

    [Fact]
    public void RemoveMissingItems_RemovesUnavailablePaths()
    {
        DropShelfViewModel viewModel = CreateViewModel();
        viewModel.AddItems([new(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt"), "Missing.txt", false)]);

        viewModel.RemoveMissingItems();

        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
    }

    [Theory]
    [InlineData(false, "\uE8A5")]
    [InlineData(true, "\uE8B7")]
    public void DropShelfItem_GlyphReflectsItemKind(bool isFolder, string expectedGlyph)
    {
        DropShelfItem item = new("C:\\Item", "Item", isFolder);

        Assert.Equal(expectedGlyph, item.Glyph);
    }

    private static DropShelfViewModel CreateViewModel() => new(new TestTextLocalizer());

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
