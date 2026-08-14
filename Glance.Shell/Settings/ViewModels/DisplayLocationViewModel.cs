using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class DisplayLocationViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, int> read,
    Action<GlanceSettings, int> write) :
    GlanceSettingsViewModel<int>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IGlanceViewModel;
