using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public sealed partial class ModulesViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<IModulesViewModel> items) :
    ObservableCollectionViewModel<IModulesViewModel>(provider, factory, messenger, disposer, items),
    ISettingViewModel;
