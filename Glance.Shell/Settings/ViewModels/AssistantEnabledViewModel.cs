using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class AssistantEnabledViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    IGlanceAssistantService assistant,
    Func<GlanceSettings, bool> read,
    Action<GlanceSettings, bool> write) :
    GlanceSettingsViewModel<bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IGlanceViewModel
{
    public IGlanceAssistantService Assistant { get; } = assistant;

    public string SettingsCategory => GlanceSettingsCategories.SpeechAndCommands;
}
