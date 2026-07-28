using Glance.Application.Abstractions;

namespace Glance.Magnifier.Tests;

public sealed class MagnifierViewModelTests
{
    [Fact]
    public void Refresh_UsesCurrentMagnification()
    {
        FakeMagnifierService service = new(new(true, 2.25));
        MagnifierViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.Refresh();

        Assert.Equal(2.25, viewModel.ZoomFactor);
        Assert.Equal("225%", viewModel.ZoomText);
        Assert.True(viewModel.CanZoomOut);
        Assert.True(viewModel.CanClose);
        Assert.Equal("Magnifier is active", viewModel.DetailText);
    }

    [Fact]
    public void Refresh_DisablesControlsWhenUnavailable()
    {
        MagnifierViewModel viewModel = new(new FakeMagnifierService(new(false, 1)), new FakeLocalizer());

        viewModel.Refresh();

        Assert.False(viewModel.IsAvailable);
        Assert.False(viewModel.CanZoomIn);
        Assert.False(viewModel.CanZoomOut);
        Assert.False(viewModel.CanClose);
        Assert.Equal("Magnifier is unavailable", viewModel.DetailText);
    }

    [Fact]
    public void ZoomIn_RequestsWindowsMagnifierShortcut()
    {
        FakeMagnifierService service = new(new(true, 1));
        MagnifierViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.ZoomIn();

        Assert.Equal(1, service.ZoomInCount);
    }

    [Fact]
    public void ZoomOut_DoesNotRequestShortcutAtNormalSize()
    {
        FakeMagnifierService service = new(new(true, 1));
        MagnifierViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.ZoomOut();

        Assert.Equal(0, service.ZoomOutCount);
    }

    [Fact]
    public void Close_RequestsWindowsMagnifierShortcutWhenActive()
    {
        FakeMagnifierService service = new(new(true, 2));
        MagnifierViewModel viewModel = new(service, new FakeLocalizer());
        viewModel.Refresh();

        viewModel.Close();

        Assert.Equal(1, service.CloseCount);
    }

    private sealed class FakeMagnifierService(MagnifierState state) :
        IMagnifierService
    {
        public int ZoomInCount { get; private set; }

        public int ZoomOutCount { get; private set; }

        public int CloseCount { get; private set; }

        public MagnifierState GetState() =>
            state;

        public bool ZoomIn()
        {
            ZoomInCount++;
            return true;
        }

        public bool ZoomOut()
        {
            ZoomOutCount++;
            return true;
        }

        public bool Close()
        {
            CloseCount++;
            return true;
        }

        public void Dispose()
        { }
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "ActiveDetail" => "Magnifier is active",
            "InactiveDetail" => "Normal size",
            "UnavailableDetail" => "Magnifier is unavailable",
            _ => key
        };
    }
}
