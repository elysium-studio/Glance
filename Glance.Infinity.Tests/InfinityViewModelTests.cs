namespace Glance.Infinity.Tests;

public sealed class InfinityViewModelTests
{
    [Fact]
    public async Task CommitEditAsyncSendsTrimmedTitleForCurrentPage()
    {
        PageTitleUpdater updater = new();
        InfinityViewModel viewModel = new(updater);
        viewModel.Update(new InfinityPageNavigationState(3, 4, "Original"));
        viewModel.BeginEditing();
        viewModel.EditingTitle = "  Updated title  ";

        await viewModel.CommitEditAsync();

        Assert.Equal(3, updater.PageIndex);
        Assert.Equal("Updated title", updater.PageTitle);
        Assert.False(viewModel.IsEditing);
    }

    [Fact]
    public async Task CommitEditAsyncKeepsEditorOpenWhenInfinityIsUnavailable()
    {
        PageTitleUpdater updater = new() { IsAvailable = false };
        InfinityViewModel viewModel = new(updater);
        viewModel.Update(new InfinityPageNavigationState(0, 1, "Original"));
        viewModel.BeginEditing();

        await viewModel.CommitEditAsync();

        Assert.True(viewModel.IsEditing);
    }

    [Fact]
    public void UpdateClosesEditorWhenInfinityMovesToAnotherPage()
    {
        InfinityViewModel viewModel = new(new PageTitleUpdater());
        viewModel.Update(new InfinityPageNavigationState(0, 1, "First"));
        viewModel.BeginEditing();

        viewModel.Update(new InfinityPageNavigationState(1, 2, "Second"));

        Assert.False(viewModel.IsEditing);
        Assert.Equal(1, viewModel.PageIndex);
        Assert.Equal("Second", viewModel.PageTitle);
    }

    [Fact]
    public void InteractionKeepsTransientModuleAvailableAfterSurfaceCloses()
    {
        InfinityViewModel viewModel = new(new PageTitleUpdater());
        viewModel.SetSurfaceVisibility(true);
        viewModel.BeginInteraction();
        viewModel.SetSurfaceVisibility(false);

        viewModel.DismissIfIdle();

        Assert.True(viewModel.IsAvailable);

        viewModel.EndInteraction();

        Assert.False(viewModel.IsAvailable);
    }

    [Fact]
    public void PointerExitKeepsPendingTitleEditUntilExplicitDismiss()
    {
        InfinityViewModel viewModel = new(new PageTitleUpdater());
        viewModel.Update(new InfinityPageNavigationState(0, 1, "Original"));
        viewModel.BeginInteraction();
        viewModel.BeginEditing();
        viewModel.EditingTitle = "Unsaved title";

        viewModel.EndInteraction();

        Assert.True(viewModel.IsEditing);
        Assert.Equal("Unsaved title", viewModel.EditingTitle);
        Assert.True(viewModel.IsAvailable);

        viewModel.CancelEditing();

        Assert.False(viewModel.IsEditing);
        Assert.Equal("Original", viewModel.EditingTitle);
        Assert.Equal("Original", viewModel.PageTitle);
        Assert.False(viewModel.IsAvailable);
    }

    private sealed class PageTitleUpdater :
        IInfinityPageTitleUpdater
    {
        public bool IsAvailable { get; set; } = true;

        public int PageIndex { get; private set; } = -1;

        public string PageTitle { get; private set; } = string.Empty;

        public ValueTask<bool> UpdatePageTitleAsync(int pageIndex, string pageTitle, CancellationToken cancellationToken = default)
        {
            PageIndex = pageIndex;
            PageTitle = pageTitle;
            return ValueTask.FromResult(IsAvailable);
        }
    }
}
