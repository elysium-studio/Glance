using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public partial class GlanceViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IEnumerable<IGlanceViewModel> items) :
    ObservableCollectionViewModel<IGlanceViewModel>(provider, factory, messenger, disposer, items),
    ISettingViewModel;
