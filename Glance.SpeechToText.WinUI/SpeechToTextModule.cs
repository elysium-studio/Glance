using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.Transcription.Windows;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SpeechToText.WinUI;

public sealed class SpeechToTextModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<SpeechToTextModule>>();
        _ = services.AddWindowsTranscription();
        _ = services.AddSingleton<ITextCopyService, WindowsTextCopyService>();
        _ = services.AddSingleton(provider => new SpeechToTextViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<SpeechToTextModule>>()));
        _ = services.AddSingleton<SpeechToTextComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<SpeechToTextComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<SpeechToTextComponent>());
    }
}
