using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsContentTemplateSelector :
    DataTemplateSelector
{
    public DataTemplate? DefaultTemplate { get; set; }

    public DataTemplate? ModuleSettingsCategoryTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is ModuleSettingsCategoryViewModel
        ? ModuleSettingsCategoryTemplate
        : DefaultTemplate;

    protected override DataTemplate? SelectTemplateCore(object item,
        DependencyObject container) => SelectTemplateCore(item);
}
