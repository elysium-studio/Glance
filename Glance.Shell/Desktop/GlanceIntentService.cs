using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceIntentService(ModulePreferenceService modulePreferences) :
    IGlanceIntentService
{
    private readonly Dictionary<string, IGlanceIntent> intents = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly ModulePreferenceService modulePreferences = modulePreferences;
    private readonly object synchronization = new();
    private Func<GlanceScreenRectangle?>? presentationTargetProvider;

    public event EventHandler<GlanceIntentInvokedEventArgs>? IntentInvoked;

    public IReadOnlyList<GlanceIntentDescriptor> GetIntents(GlanceContentKind kind)
    {
        HashSet<string> activeComponents =
        [
            with(StringComparer.OrdinalIgnoreCase),
            .. modulePreferences.GetActiveComponents().Select(component => component.Id)
        ];

        lock (synchronization)
        {
            return
            [
                .. intents.Values
                    .Where(intent => activeComponents.Contains(intent.Descriptor.TargetComponentId) && intent.CanHandle(kind))
                    .Select(intent => intent.Descriptor)
                    .OrderByDescending(intent => intent.MatchPriority)
                    .ThenBy(intent => intent.DisplayName)
            ];
        }
    }

    public IReadOnlyList<GlanceIntentDescriptor> GetIntents(GlanceContentContext context)
    {
        lock (synchronization)
        {
            return
            [
                .. intents.Values
                    .Where(intent => modulePreferences.IsEnabled(intent.Descriptor.TargetComponentId) && intent.CanHandle(context))
                    .Select(intent => intent.Descriptor)
                    .OrderByDescending(intent => intent.MatchPriority)
                    .ThenBy(intent => intent.DisplayName)
            ];
        }
    }

    public GlanceScreenRectangle? GetPresentationTarget() => presentationTargetProvider?.Invoke();

    public async Task<bool> InvokeAsync(string intentId,
        GlanceContentContext context,
        CancellationToken cancellationToken = default)
    {
        IGlanceIntent? intent;

        lock (synchronization)
        {
            _ = intents.TryGetValue(intentId, out intent);
        }

        if (intent is null ||
            !intent.CanHandle(context) ||
            !modulePreferences.IsEnabled(intent.Descriptor.TargetComponentId))
        {
            return false;
        }

        await intent.InvokeAsync(context, cancellationToken);
        IntentInvoked?.Invoke(this, new GlanceIntentInvokedEventArgs(intent.Descriptor.TargetComponentId));
        return true;
    }

    public void Register(IEnumerable<IGlanceIntent> advertisedIntents)
    {
        IGlanceIntent[] registrations = [.. advertisedIntents];

        lock (synchronization)
        {
            string? duplicateId = registrations
                .Select(intent => intent.Descriptor.Id)
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1 || intents.ContainsKey(group.Key))
                .Select(group => group.Key)
                .FirstOrDefault();

            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"A Glance intent with the identifier '{duplicateId}' is already registered.");
            }

            foreach (IGlanceIntent intent in registrations)
            {
                intents.Add(intent.Descriptor.Id, intent);
            }
        }
    }

    public void Unregister(IEnumerable<IGlanceIntent> advertisedIntents)
    {
        HashSet<IGlanceIntent> removals = [.. advertisedIntents];

        lock (synchronization)
        {
            foreach (string id in intents.Where(item => removals.Contains(item.Value)).Select(item => item.Key).ToArray())
            {
                _ = intents.Remove(id);
            }
        }
    }

    public void SetPresentationTargetProvider(Func<GlanceScreenRectangle?>? provider) => presentationTargetProvider = provider;
}
