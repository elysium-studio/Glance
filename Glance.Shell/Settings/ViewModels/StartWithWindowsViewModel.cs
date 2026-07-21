using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public partial class StartWithWindowsViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, bool> read,
    Action<GlanceSettings, bool> write) :
    ObservableReadWriteViewModel<GlanceSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IWindowsViewModel;
