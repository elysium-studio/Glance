using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceModuleCategoryResolver(ITextLocalizer localizer) :
    IGlanceModuleCategoryResolver
{
    public GlanceModuleCategoryDescriptor Resolve(IGlanceComponent? component)
    {
        if (component is IGlanceModuleCategoryProvider provider)
        {
            GlanceModuleCategoryDescriptor category = provider.ModuleCategory;

            if (!string.IsNullOrWhiteSpace(category.Id) && !string.IsNullOrWhiteSpace(category.DisplayName))
            {
                return category;
            }
        }

        return Resolve(component?.SettingsCategory ?? GlanceModuleCategories.Other);
    }

    public GlanceModuleCategoryDescriptor Resolve(GlanceModuleFeedItem module)
    {
        GlanceModuleCategoryDescriptor descriptor = Resolve(module.Category);
        string displayName = string.IsNullOrWhiteSpace(module.CategoryDisplayName) ? descriptor.DisplayName : module.CategoryDisplayName;
        string glyph = string.IsNullOrWhiteSpace(module.CategoryGlyph) ? descriptor.Glyph : module.CategoryGlyph;
        int order = module.CategoryOrder == 0 ? descriptor.Order : module.CategoryOrder;
        return new(module.Category, displayName, glyph, order);
    }

    private GlanceModuleCategoryDescriptor Resolve(string id) => id switch
    {
        GlanceModuleCategories.Information => new(id, localizer.GetText("InformationModulesTitle"), "\uE946", 100),
        GlanceModuleCategories.Productivity => new(id, localizer.GetText("ProductivityModulesTitle"), "\uE8FD", 200),
        GlanceModuleCategories.Health => new(id, localizer.GetText("HealthModulesTitle"), "\uE95E", 300),
        GlanceModuleCategories.MediaAndCapture => new(id, localizer.GetText("MediaAndCaptureModulesTitle"), "\uE8B9", 400),
        GlanceModuleCategories.DevicesAndSystem => new(id, localizer.GetText("DevicesAndSystemModulesTitle"), "\uE772", 500),
        GlanceModuleCategories.Integrations => new(id, localizer.GetText("IntegrationsModulesTitle"), "\uE71B", 600),
        GlanceModuleCategories.Other => new(id, localizer.GetText("OtherModulesTitle"), "\uE8B7", 1000),
        _ => new(id, id, "\uE8B7", 900)
    };
}
