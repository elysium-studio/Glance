using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public partial class PlacementViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, int> read,
    Action<GlanceSettings, int> write) :
    ObservableReadWriteViewModel<GlanceSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IGlanceViewModel;
