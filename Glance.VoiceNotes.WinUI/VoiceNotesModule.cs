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
        services.AddSingleton<ModuleResourceTextLocalizer<VoiceNotesModule>>();
        services.AddSingleton<IVoiceRecordingService>(provider =>
            new WindowsVoiceRecordingService(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Glance",
                "VoiceNotes")));
        services.AddSingleton(provider => new VoiceNotesViewModel(
            provider.GetRequiredService<ModuleResourceTextLocalizer<VoiceNotesModule>>()));
        services.AddSingleton<IGlanceComponent, VoiceNotesComponent>();
    }
}
