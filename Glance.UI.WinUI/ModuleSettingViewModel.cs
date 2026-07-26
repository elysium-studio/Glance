using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;
using System;

namespace Glance.UI.WinUI;

public abstract partial class ModuleSettingViewModel<TOptions, TValue>(IServiceProvider provider,
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
    ObservableReadWriteViewModel<TOptions, TValue>(provider, factory, messenger, disposer, dispatcher, options, writer, read, write),
    IGlanceModuleSettingViewModel
    where TOptions : class, new()
{
    public string ModuleId { get; } = moduleId;

    public int Order { get; } = order;
}
