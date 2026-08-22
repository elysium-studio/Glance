using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceModuleCategoryResolverTests
{
    private readonly GlanceModuleCategoryResolver resolver = new(new TestLocalizer());

    [Fact]
    public void ResolvesBuiltInHealthCategory()
    {
        GlanceModuleCategoryDescriptor category = resolver.Resolve(new HealthComponent());

        Assert.Equal(GlanceModuleCategories.Health, category.Id);
        Assert.Equal("HealthModulesTitle", category.DisplayName);
        Assert.Equal(300, category.Order);
    }

    [Fact]
    public void ResolvesCategoryProvidedByThirdPartyComponent()
    {
        GlanceModuleCategoryDescriptor category = resolver.Resolve(new CustomCategoryComponent());

        Assert.Equal("Research", category.Id);
        Assert.Equal("Research and labs", category.DisplayName);
        Assert.Equal("\uE773", category.Glyph);
        Assert.Equal(450, category.Order);
    }

    private sealed class HealthComponent :
        TestComponent
    {
        public override string SettingsCategory => GlanceModuleCategories.Health;
    }

    private sealed class CustomCategoryComponent :
        TestComponent,
        IGlanceModuleCategoryProvider
    {
        public GlanceModuleCategoryDescriptor ModuleCategory { get; } = new("Research", "Research and labs", "\uE773", 450);
    }

    private abstract class TestComponent :
        IGlanceComponent
    {
        public string Id => GetType().Name;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public virtual string SettingsCategory => GlanceModuleCategories.Other;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();
    }

    private sealed class TestLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
    }
}
