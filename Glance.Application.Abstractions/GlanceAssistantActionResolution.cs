using System.Text.Json;

namespace Glance.Application.Abstractions;

public sealed record GlanceAssistantActionResolution(string ActionId,
    JsonElement Arguments,
    string? Response = null)
{
    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    public GlanceAssistantActionResolution(string actionId,
        string? response = null) :
        this(actionId, EmptyArguments, response)
    { }
}
