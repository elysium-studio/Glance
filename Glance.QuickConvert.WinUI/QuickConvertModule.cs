using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Glance.QuickConvert.WinUI;

public sealed class QuickConvertModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<QuickConvertModule>>();
        _ = services.AddSingleton(provider => new QuickConvertViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<QuickConvertModule>>()));
        _ = services.AddSingleton<IGlanceComponent, QuickConvertComponent>();
        _ = services.AddViewFor<QuickConverterSettingsView, IGlanceModuleSettingViewModel, QuickConverterSettingsViewModel>(ServiceLifetime.Transient, provider => new QuickConverterSettingsView(), provider => new QuickConverterSettingsViewModel(provider.GetRequiredService<IGlanceQuickConverterManager>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ModuleResourceTextLocalizer<QuickConvertModule>>()));
        _ = services.AddSingleton<IGlanceIntent>(provider => new QuickConvertIntentAdapter(provider
            .GetServices<IGlanceComponent>()
            .OfType<QuickConvertComponent>()
            .Single()));
    }

    private sealed class QuickConvertIntentAdapter(QuickConvertComponent component) :
        IGlanceIntent
    {
        public GlanceIntentDescriptor Descriptor => component.Descriptor;

        public bool CanHandle(GlanceContentKind kind) => component.CanHandle(kind);

        public bool CanHandle(GlanceContentContext context) => component.CanHandle(context);

        public Task InvokeAsync(GlanceContentContext context,
            CancellationToken cancellationToken = default) =>
            ((IGlanceIntent)component).InvokeAsync(context, cancellationToken);

        public Task<bool> TryInvokeAsync(GlanceContentContext context,
            CancellationToken cancellationToken = default) =>
            ((IGlanceIntent)component).TryInvokeAsync(context, cancellationToken);
    }
}
