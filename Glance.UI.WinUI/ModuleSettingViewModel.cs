using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;
using System;

namespace Glance.UI.WinUI;

public abstract partial class ModuleSettingViewModel<TOptions, TValue> :
    ObservableReadWriteViewModel<TOptions, TValue>,
    IGlanceModuleSettingViewModel
    where TOptions : class, new()
{
    protected ModuleSettingViewModel(
        IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        TOptions options,
        IWritableOptions<TOptions> writer,
        string moduleId,
        int order,
        Func<TOptions, TValue?> read,
        Action<TOptions, TValue?> write) :
        base(provider, factory, messenger, disposer, dispatcher, options, writer, read, write)
    {
        ModuleId = moduleId;
        Order = order;
    }

    public string ModuleId { get; }

    public int Order { get; }
}
