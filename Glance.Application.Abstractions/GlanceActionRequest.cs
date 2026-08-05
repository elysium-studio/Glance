using System.Text.Json;

namespace Glance.Application.Abstractions;

public sealed record GlanceActionRequest(string ActionId,
    JsonElement Arguments,
    bool IsConfirmed = false)
{
    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    public GlanceActionRequest(string actionId,
        bool isConfirmed = false) :
        this(actionId, EmptyArguments, isConfirmed)
    { }

    public bool TryGetArgument(string name,
        out JsonElement value)
    {
        if (Arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in Arguments.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public string? GetString(string name) => TryGetArgument(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public double? GetNumber(string name) => TryGetArgument(name, out JsonElement value) && value.TryGetDouble(out double number)
            ? number
            : null;

    public long? GetInteger(string name) => TryGetArgument(name, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : null;

    public bool? GetBoolean(string name) => TryGetArgument(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
