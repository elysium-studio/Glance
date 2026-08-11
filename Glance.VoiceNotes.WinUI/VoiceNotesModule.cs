using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.VoiceNotes.WinUI;

public sealed class VoiceNotesModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<VoiceNotesSettings>("VoiceNotes", "voice-notes.settings.dat", VoiceNotesJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<VoiceNotesModule>>();
        _ = services.AddSingleton(new VoiceNoteRepository(GlanceModuleData.GetPath("VoiceNotes", "voice-notes.db")));
        _ = services.AddSingleton<IVoiceRecordingService>(provider =>
            new WindowsVoiceRecordingService(GlanceModuleData.GetDirectory("VoiceNotes"), provider.GetRequiredService<VoiceNoteRepository>()));
        _ = services.AddSingleton(provider => new VoiceNotesViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<VoiceNotesModule>>(), provider.GetRequiredService<GlanceModuleOptions<VoiceNotesSettings>>().Current));
        _ = services.AddSingleton<VoiceNotesComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<VoiceNotesComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<VoiceNotesComponent>());
        _ = services.AddViewFor<RecentRecordingLimitSettingView, IGlanceModuleSettingViewModel, RecentRecordingLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentRecordingLimitSettingView(), provider => new RecentRecordingLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<VoiceNotesSettings>>().Current, provider.GetRequiredService<IWritableOptions<VoiceNotesSettings>>()));
    }
}
