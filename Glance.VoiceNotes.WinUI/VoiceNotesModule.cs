using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.VoiceNotes.WinUI;

public sealed class VoiceNotesModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<VoiceNotesSettings>("VoiceNotes", "voice-notes.settings.dat", VoiceNotesJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<VoiceNotesModule>>();
        services.AddSingleton<IVoiceRecordingService>(provider =>
            new WindowsVoiceRecordingService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "VoiceNotes")));
        services.AddSingleton(provider => new VoiceNotesViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<VoiceNotesModule>>(), provider.GetRequiredService<GlanceModuleOptions<VoiceNotesSettings>>().Current));
        services.AddSingleton<IGlanceComponent, VoiceNotesComponent>();
        services.AddViewFor<RecentRecordingLimitSettingView, IGlanceModuleSettingViewModel, RecentRecordingLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentRecordingLimitSettingView(), provider => new RecentRecordingLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<VoiceNotesSettings>>().Current, provider.GetRequiredService<IWritableOptions<VoiceNotesSettings>>()));
    }
}
