using Elysium.Presentation.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public sealed class GlanceContentDialogHandler :
    INavigationHandler
{
    public async Task HandleAsync(object view,
        object? viewModel,
        NavigationParameters parameters)
    {
        if (view is not ContentDialog dialog)
        {
            return;
        }

        if (viewModel is not null)
        {
            dialog.DataContext = viewModel;
        }

        XamlRoot? xamlRoot = parameters.Get<XamlRoot>("XamlRoot");

        if (xamlRoot is not null)
        {
            dialog.XamlRoot = xamlRoot;
        }

        _ = await dialog.ShowAsync();
    }
}
