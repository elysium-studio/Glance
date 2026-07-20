using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Glance.Tests;

public sealed class LocalizationResourceTests
{
    private static readonly string[] ExpectedModules =
    [
        "Clipboard",
        "DropShelf",
        "Media",
        "Power",
        "Shell",
        "Stopwatch",
        "SystemMonitor",
        "Timer",
    ];

    private static readonly string[] ExpectedLanguages =
    [
        "de",
        "en",
        "es",
        "fr",
        "it",
        "ja",
        "ko",
        "nl",
        "pl",
        "pt-BR",
        "qps-ploc",
        "zh-Hans",
    ];

    [Fact]
    public void ModuleResourcesMatchEnglishContracts()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "LocalizationResources");
        string[] modules = GetDirectories(root);

        Assert.Equal(ExpectedModules, modules);

        foreach (string module in modules)
        {
            string moduleRoot = Path.Combine(root, module);
            string[] languages = GetDirectories(moduleRoot);
            Assert.Equal(ExpectedLanguages, languages);

            IReadOnlyList<ResourceEntry> english = ReadResources(
                Path.Combine(moduleRoot, "en", "Resources.resw"));

            foreach (string language in languages.Where(language => language != "en"))
            {
                IReadOnlyList<ResourceEntry> localized = ReadResources(
                    Path.Combine(moduleRoot, language, "Resources.resw"));

                Assert.Equal(
                    english.Select(entry => entry.Name),
                    localized.Select(entry => entry.Name));

                for (int index = 0; index < english.Count; index++)
                {
                    Assert.Equal(
                        GetPlaceholders(english[index].Value),
                        GetPlaceholders(localized[index].Value));

                    if (english[index].Value.Contains("Glance", StringComparison.Ordinal))
                    {
                        Assert.Contains(
                            "Glance",
                            localized[index].Value,
                            StringComparison.Ordinal);
                    }
                }
            }
        }
    }

    private static string[] GetDirectories(string path) =>
        Directory.GetDirectories(path)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order()
            .ToArray();

    private static IReadOnlyList<ResourceEntry> ReadResources(string path) =>
        XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => new ResourceEntry(
                element.Attribute("name")!.Value,
                element.Element("value")!.Value))
            .ToArray();

    private static string[] GetPlaceholders(string value) =>
        Regex.Matches(value, "\\{\\d+\\}")
            .Select(match => match.Value)
            .Order()
            .ToArray();

    private sealed record ResourceEntry(string Name, string Value);
}
