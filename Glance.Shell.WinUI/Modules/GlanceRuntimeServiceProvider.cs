using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Glance.Shell.WinUI;

internal sealed class GlanceRuntimeServiceProvider :
    IServiceProvider
{
    private readonly List<IServiceProvider> moduleProviders = [];
    private readonly object synchronization = new();
    private readonly IViewFactory viewFactory;
    private readonly IViewModelFactory viewModelFactory;
    private readonly IServiceProvider applicationServices;

    public GlanceRuntimeServiceProvider(IServiceProvider applicationServices)
    {
        this.applicationServices = applicationServices;
        viewFactory = new ViewFactory(CreateView);
        viewModelFactory = new ViewModelFactory(CreateViewModel);
    }

    public void AddModuleProvider(IServiceProvider provider)
    {
        lock (synchronization)
        {
            moduleProviders.Add(provider);
        }
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IViewFactory))
        {
            return viewFactory;
        }

        if (serviceType == typeof(IViewModelFactory))
        {
            return viewModelFactory;
        }

        return applicationServices.GetService(serviceType);
    }

    private object? CreateView(string key, object?[]? arguments)
    {
        foreach (IServiceProvider provider in GetModuleProviders())
        {
            object? view = provider.GetService<IViewFactory>()?.Create(key, arguments);

            if (view is not null)
            {
                return view;
            }
        }

        return applicationServices.GetService<IViewFactory>()?.Create(key, arguments);
    }

    private object CreateViewModel(string key, object?[]? arguments)
    {
        string normalizedKey = key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length] : key;

        foreach (IServiceProvider provider in GetModuleProviders())
        {
            if (provider.GetKeyedService<ViewDescriptor>(normalizedKey) is not null)
            {
                return provider.GetRequiredService<IViewModelFactory>().Create(key, arguments);
            }
        }

        return applicationServices.GetRequiredService<IViewModelFactory>().Create(key, arguments);
    }

    private IReadOnlyList<IServiceProvider> GetModuleProviders()
    {
        lock (synchronization)
        {
            return moduleProviders.ToArray();
        }
    }
}
