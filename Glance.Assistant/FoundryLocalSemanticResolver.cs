using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Betalgo.Ranul.OpenAI.ObjectModels.SharedModels;
using Glance.Application.Abstractions;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Glance.Assistant;

public sealed class FoundryLocalSemanticResolver(ILogger<FoundryLocalSemanticResolver> logger) :
    IGlanceAssistantSemanticResolver,
    IAsyncDisposable
{
    private const string ModelAlias = "qwen2.5-0.5b";
    private const string NotUnderstoodToolName = "glance_not_understood";
    private readonly SemaphoreSlim modelGate = new(1, 1);
    private readonly SemaphoreSlim resolutionGate = new(1, 1);
    private OpenAIChatClient? chatClient;
    private IModel? model;

    public string Id => "FoundryLocal";

    public string DisplayName => "Microsoft Foundry Local";

    public async Task<GlanceAssistantActionResolution?> ResolveAsync(string command,
        IReadOnlyList<GlanceActionDescriptor> actions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command) || actions.Count == 0)
        {
            return null;
        }

        await resolutionGate.WaitAsync(cancellationToken);

        try
        {
            AssistantWakeDiagnostics.Write("Semantic.Resolve.Begin", $"Command={command}; Actions={actions.Count}");
            OpenAIChatClient client = await GetChatClientAsync(cancellationToken);
            IReadOnlyList<GlanceActionDescriptor> candidates = SelectCandidates(command, actions);

            if (candidates.Count == 0)
            {
                AssistantWakeDiagnostics.Write("Semantic.Resolve.NotUnderstood", $"Command={command}; Reason=NoRelevantActions");
                return null;
            }

            AssistantWakeDiagnostics.Write("Semantic.Resolve.Candidates", $"Command={command}; Actions={string.Join(',', candidates.Select(action => action.Id))}");
            Dictionary<string, GlanceActionDescriptor> actionMap = [];
            List<ToolDefinition> tools = CreateTools(candidates, actionMap);
            List<ChatMessage> messages =
            [
                new()
                {
                    Role = "system",
                    Content = $"You interpret spoken requests for Glance. The current local date and time is {DateTimeOffset.Now:O}. Tool names, semantic tags, and example utterances describe their actions. The transcript may begin with a distorted fragment of the wake phrase 'Hey Glance'; treat that fragment as conversational noise. It may also contain speech-recognition mistakes, split words, homophones, or spoken numbers. Infer the user's intended meaning and call exactly one matching tool. Match both the requested operation and its subject; never select an unrelated tool merely because one word sounds similar. Convert spoken quantities to numeric arguments and resolve relative dates such as today or tomorrow against the current local date and time. Never invent an action or argument. If the request does not clearly map to an available tool, call glance_not_understood."
                },
                new() { Role = "user", Content = command }
            ];

            ChatCompletionCreateResponse completion = await client.CompleteChatAsync(messages, tools, cancellationToken);
            FunctionCall? call = completion.Choices?.FirstOrDefault()?.Message.ToolCalls?.FirstOrDefault()?.FunctionCall;

            if (call is null || string.Equals(call.Name, NotUnderstoodToolName, StringComparison.Ordinal))
            {
                AssistantWakeDiagnostics.Write("Semantic.Resolve.NotUnderstood", $"Command={command}; Tool={call?.Name ?? "<none>"}");
                return null;
            }

            if (!actionMap.TryGetValue(call.Name ?? string.Empty, out GlanceActionDescriptor? action))
            {
                logger.LogWarning("Foundry Local selected unknown Glance tool {AssistantTool}", call.Name);
                AssistantWakeDiagnostics.Write("Semantic.Resolve.UnknownTool", $"Command={command}; Tool={call.Name}");
                return null;
            }

            using JsonDocument arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments);
            AssistantWakeDiagnostics.Write("Semantic.Resolve.Selected", $"Command={command}; Tool={call.Name}; Action={action.Id}; Arguments={arguments.RootElement.GetRawText()}");
            return new GlanceAssistantActionResolution(action.Id, arguments.RootElement.Clone());
        }
        finally
        {
            resolutionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await modelGate.WaitAsync();

        try
        {
            if (model is not null)
            {
                await model.UnloadAsync();
                model = null;
                chatClient = null;
            }
        }
        finally
        {
            modelGate.Release();
            modelGate.Dispose();
            resolutionGate.Dispose();
        }
    }

    private async Task<OpenAIChatClient> GetChatClientAsync(CancellationToken cancellationToken)
    {
        if (chatClient is not null)
        {
            return chatClient;
        }

        await modelGate.WaitAsync(cancellationToken);

        try
        {
            if (chatClient is not null)
            {
                return chatClient;
            }

            await FoundryLocalRuntime.EnsureInitializedAsync(logger, cancellationToken);
            ICatalog catalog = await FoundryLocalManager.Instance.GetCatalogAsync(cancellationToken);
            model = await catalog.GetModelAsync(ModelAlias, cancellationToken) ?? throw new InvalidOperationException("The Microsoft Foundry Local command model is unavailable");
            await model.DownloadAsync(_ => { }, cancellationToken);
            await model.LoadAsync(cancellationToken);
            chatClient = await model.GetChatClientAsync(cancellationToken);
            chatClient.Settings.Temperature = 0;
            chatClient.Settings.MaxTokens = 128;
            chatClient.Settings.ToolChoice = ToolChoice.Required;
            return chatClient;
        }
        finally
        {
            modelGate.Release();
        }
    }

    private static List<ToolDefinition> CreateTools(IReadOnlyList<GlanceActionDescriptor> actions,
        Dictionary<string, GlanceActionDescriptor> actionMap)
    {
        List<ToolDefinition> tools = [];

        for (int index = 0; index < actions.Count; index++)
        {
            GlanceActionDescriptor action = actions[index];
            string toolName = CreateToolName(action.Id);

            if (actionMap.ContainsKey(toolName))
            {
                toolName = $"{toolName}_{index:D3}";
            }

            actionMap.Add(toolName, action);
            tools.Add(new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = toolName,
                    Description = CreateToolDescription(action),
                    Parameters = CreateParameters(action.Parameters)
                }
            });
        }

        tools.Add(new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = NotUnderstoodToolName,
                Description = "Use only when the spoken request does not clearly match any available Glance action.",
                Parameters = new PropertyDefinition
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertyDefinition>(),
                    Required = [],
                    AdditionalProperties = false
                }
            }
        });
        return tools;
    }

    private static IReadOnlyList<GlanceActionDescriptor> SelectCandidates(string command,
        IReadOnlyList<GlanceActionDescriptor> actions)
    {
        string[] commandTerms = [.. Tokenize(command).Where(term => !IgnoredTerms.Contains(term))];

        if (commandTerms.Length == 0)
        {
            return [];
        }

        var components = actions
            .GroupBy(action => action.TargetComponentId)
            .Select(group => new
            {
                Id = group.Key,
                Score = Score(commandTerms, group.SelectMany(GetSemanticText))
            })
            .OrderByDescending(component => component.Score)
            .ToArray();
        int bestScore = components.FirstOrDefault()?.Score ?? 0;

        if (bestScore < 4)
        {
            return [];
        }

        HashSet<string> componentIds = [with(StringComparer.OrdinalIgnoreCase), .. components
            .Where(component => component.Score >= bestScore - 2)
            .Take(3)
            .Select(component => component.Id)];
        var candidateActions = actions
            .Where(action => componentIds.Contains(action.TargetComponentId))
            .Select(action => new
            {
                Action = action,
                Score = Score(commandTerms, GetActionSemanticText(action))
            })
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();
        int bestActionScore = candidateActions.FirstOrDefault()?.Score ?? 0;

        if (bestActionScore < 4)
        {
            return [];
        }

        return [.. candidateActions
            .Where(candidate => candidate.Score >= bestActionScore - 2)
            .Select(candidate => candidate.Action)];
    }

    private static IEnumerable<string> GetSemanticText(GlanceActionDescriptor action)
    {
        yield return action.Id;
        yield return action.TargetComponentId;
        yield return action.DisplayName;
        yield return action.Description;

        foreach (string tag in action.SemanticTags)
        {
            yield return tag;
        }

        foreach (string example in action.ExampleUtterances)
        {
            yield return example;
        }
    }

    private static IEnumerable<string> GetActionSemanticText(GlanceActionDescriptor action)
    {
        yield return action.DisplayName;
        yield return action.Description;

        foreach (string tag in action.SemanticTags)
        {
            yield return tag;
        }

        foreach (string example in action.ExampleUtterances)
        {
            yield return example;
        }
    }

    private static int Score(IReadOnlyList<string> commandTerms, IEnumerable<string> semanticText)
    {
        string[] actionTerms = [.. semanticText.SelectMany(Tokenize).Distinct(StringComparer.Ordinal)];
        int score = 0;

        foreach (string commandTerm in commandTerms)
        {
            int termScore = 0;

            foreach (string actionTerm in actionTerms)
            {
                if (commandTerm == actionTerm)
                {
                    termScore = Math.Max(termScore, 4);
                }
                else if (commandTerm.Length >= 4 && actionTerm.Length >= 4 &&
                    (commandTerm.StartsWith(actionTerm, StringComparison.Ordinal) || actionTerm.StartsWith(commandTerm, StringComparison.Ordinal)))
                {
                    termScore = Math.Max(termScore, 3);
                }
                else if (commandTerm.Length >= 4 && actionTerm.Length >= 4 && EditDistance(commandTerm, actionTerm) <= 1)
                {
                    termScore = Math.Max(termScore, 2);
                }
            }

            score += termScore;
        }

        return score;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        StringBuilder term = new();

        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                term.Append(char.ToLowerInvariant(character));
            }
            else if (term.Length > 0)
            {
                yield return term.ToString();
                term.Clear();
            }
        }

        if (term.Length > 0)
        {
            yield return term.ToString();
        }
    }

    private static int EditDistance(string left, string right)
    {
        int[] previous = [.. Enumerable.Range(0, right.Length + 1)];
        int[] current = new int[right.Length + 1];

        for (int leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;

            for (int rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                int substitution = previous[rightIndex - 1] + (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1);
                current[rightIndex] = Math.Min(Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static readonly HashSet<string> IgnoredTerms =
    [
        "a", "an", "and", "can", "could", "for", "i", "me", "my", "please", "the", "to", "you", "would"
    ];

    private static string CreateToolName(string actionId)
    {
        StringBuilder name = new("glance_");

        for (int index = 0; index < actionId.Length; index++)
        {
            char character = actionId[index];

            if (!char.IsLetterOrDigit(character))
            {
                if (name[^1] != '_')
                {
                    name.Append('_');
                }

                continue;
            }

            if (char.IsUpper(character) && index > 0 && char.IsLower(actionId[index - 1]) && name[^1] != '_')
            {
                name.Append('_');
            }

            name.Append(char.ToLowerInvariant(character));
        }

        return name.ToString().TrimEnd('_');
    }

    private static string CreateToolDescription(GlanceActionDescriptor action)
    {
        StringBuilder description = new();
        description.Append(action.DisplayName);
        description.Append(". ");
        description.Append(action.Description);

        if (action.SemanticTags.Count > 0)
        {
            description.Append(" Semantic tags: ");
            description.Append(string.Join(", ", action.SemanticTags));
            description.Append('.');
        }

        if (action.ExampleUtterances.Count > 0)
        {
            description.Append(" Example requests: ");
            description.Append(string.Join("; ", action.ExampleUtterances.Select(example => $"'{example}'")));
            description.Append('.');
        }

        return description.ToString();
    }

    private static PropertyDefinition CreateParameters(IReadOnlyList<GlanceActionParameterDescriptor> parameters) =>
        new()
        {
            Type = "object",
            Properties = parameters.ToDictionary(parameter => parameter.Name, CreateParameter),
            Required = [.. parameters.Where(parameter => parameter.IsRequired).Select(parameter => parameter.Name)],
            AdditionalProperties = false
        };

    private static PropertyDefinition CreateParameter(GlanceActionParameterDescriptor parameter) =>
        new()
        {
            Type = parameter.Type switch
            {
                GlanceActionParameterType.String => "string",
                GlanceActionParameterType.Integer => "integer",
                GlanceActionParameterType.Number => "number",
                GlanceActionParameterType.Boolean => "boolean",
                _ => "string"
            },
            Description = parameter.Description,
            Enum = parameter.AllowedValues is null ? null : [.. parameter.AllowedValues],
            Minimum = (float?)parameter.Minimum,
            Maximum = (float?)parameter.Maximum
        };
}
