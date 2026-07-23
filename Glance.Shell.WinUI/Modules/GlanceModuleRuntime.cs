using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceModuleRuntime :
    IAsyncDisposable
{
    private readonly IReadOnlyList<IHostedService> hostedServices;

    private GlanceModuleRuntime(
        ServiceProvider services,
        IReadOnlyList<IHostedService> hostedServices)
    {
        Services = services;
        this.hostedServices = hostedServices;
    }

    public ServiceProvider Services { get; }

    public static async Task<GlanceModuleRuntime> CreateAsync(
        IServiceProvider applicationServices,
        IReadOnlyList<IGlanceModule> modules,
        CancellationToken cancellationToken = default)
    {
        ServiceCollection registrations = [];
        AddModuleInfrastructure(registrations);

        foreach (IGlanceModule module in modules)
        {
            module.Register(registrations);
        }

        ServiceCollection isolatedRegistrations = [];

        foreach (ServiceDescriptor descriptor in registrations)
        {
            ((ICollection<ServiceDescriptor>)isolatedRegistrations).Add(RewriteDescriptor(descriptor, applicationServices));
        }

        ServiceProvider services = isolatedRegistrations.BuildServiceProvider();
        IReadOnlyList<IHostedService> hostedServices = services.GetServices<IHostedService>().ToArray();
        List<IHostedService> startedServices = [];

        try
        {
            foreach (IHostedService hostedService in hostedServices)
            {
                await hostedService.StartAsync(cancellationToken);
                startedServices.Add(hostedService);
            }

            return new GlanceModuleRuntime(services, hostedServices);
        }
        catch
        {
            foreach (IHostedService hostedService in startedServices.AsEnumerable().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            await services.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IHostedService hostedService in hostedServices.Reverse())
        {
            try
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
            catch
            {
            }
        }

        await Services.DisposeAsync();
    }

    private static void AddModuleInfrastructure(IServiceCollection services)
    {
        services
            .AddSingleton<IViewModelFactory>(provider => new ViewModelFactory((key, arguments) =>
            {
                string normalizedKey = NormalizeViewKey(key);
                Type type = provider.GetRequiredKeyedService<ViewDescriptor>(normalizedKey).ViewModelType!;

                if (arguments is { Length: > 0 })
                {
                    return provider.GetRequiredService<IServiceFactory>().Create(type, arguments);
                }

                return provider.GetRequiredKeyedService(type, normalizedKey)!;
            }))
            .AddSingleton<IViewFactory>(provider => new ViewFactory((key, arguments) =>
            {
                string normalizedKey = NormalizeViewKey(key);
                Type? type = provider.GetKeyedService<ViewDescriptor>(normalizedKey)?.ViewType;

                if (type is null)
                {
                    return null;
                }

                if (arguments is { Length: > 0 })
                {
                    return provider.GetRequiredService<IServiceFactory>().Create(type, arguments);
                }

                return provider.GetKeyedService(type, normalizedKey);
            }))
            .AddServiceFactory();
    }

    private static string NormalizeViewKey(string key) =>
        key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length] : key;

    private static ServiceDescriptor RewriteDescriptor(
        ServiceDescriptor descriptor,
        IServiceProvider applicationServices)
    {
        if (descriptor.IsKeyedService)
        {
            if (descriptor.KeyedImplementationInstance is object instance)
            {
                return new ServiceDescriptor(descriptor.ServiceType, descriptor.ServiceKey, instance);
            }

            if (descriptor.KeyedImplementationFactory is { } factory)
            {
                return new ServiceDescriptor(descriptor.ServiceType, descriptor.ServiceKey, (provider, key) => factory(new ParentFallbackServiceProvider(provider, applicationServices), key), descriptor.Lifetime);
            }

            Type implementationType = descriptor.KeyedImplementationType!;
            return new ServiceDescriptor(descriptor.ServiceType, descriptor.ServiceKey, (provider, _) => ActivatorUtilities.CreateInstance(new ParentFallbackServiceProvider(provider, applicationServices), implementationType), descriptor.Lifetime);
        }

        if (descriptor.ImplementationInstance is object implementationInstance)
        {
            return new ServiceDescriptor(descriptor.ServiceType, implementationInstance);
        }

        if (descriptor.ImplementationFactory is { } implementationFactory)
        {
            return new ServiceDescriptor(descriptor.ServiceType, provider => implementationFactory(new ParentFallbackServiceProvider(provider, applicationServices)), descriptor.Lifetime);
        }

        Type type = descriptor.ImplementationType!;
        return new ServiceDescriptor(descriptor.ServiceType, provider => ActivatorUtilities.CreateInstance(new ParentFallbackServiceProvider(provider, applicationServices), type), descriptor.Lifetime);
    }

    private sealed class ParentFallbackServiceProvider(
        IServiceProvider moduleServices,
        IServiceProvider applicationServices) :
        IServiceProvider,
        IKeyedServiceProvider,
        IServiceProviderIsService,
        IServiceProviderIsKeyedService
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
            {
                return this;
            }

            return moduleServices.GetService(serviceType) ?? applicationServices.GetService(serviceType);
        }

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            (moduleServices as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey) ??
            (applicationServices as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey);

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey) ??
            throw new InvalidOperationException($"No keyed service for type '{serviceType}' and key '{serviceKey}' has been registered.");

        public bool IsService(Type serviceType) =>
            (moduleServices.GetService<IServiceProviderIsService>()?.IsService(serviceType) ?? false) ||
            (applicationServices.GetService<IServiceProviderIsService>()?.IsService(serviceType) ?? false);

        public bool IsKeyedService(Type serviceType, object? serviceKey) =>
            (moduleServices.GetService<IServiceProviderIsKeyedService>()?.IsKeyedService(serviceType, serviceKey) ?? false) ||
            (applicationServices.GetService<IServiceProviderIsKeyedService>()?.IsKeyedService(serviceType, serviceKey) ?? false);
    }
}
