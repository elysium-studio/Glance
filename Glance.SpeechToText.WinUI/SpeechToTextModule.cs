using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SpeechToText.WinUI;

public sealed class SpeechToTextModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<SpeechToTextModule>>();
        services.AddSingleton<ISpeechRecognitionService, WindowsSpeechRecognitionService>();
        services.AddSingleton<ITextCopyService, WindowsTextCopyService>();
        services.AddSingleton(provider => new SpeechToTextViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<SpeechToTextModule>>()));
        services.AddSingleton<SpeechToTextComponent>();
        services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<SpeechToTextComponent>());
        services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<SpeechToTextComponent>());
    }
}
