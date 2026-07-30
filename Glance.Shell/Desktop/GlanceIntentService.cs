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
                    .OrderBy(intent => intent.DisplayName)
            ];
        }
    }

    public GlanceScreenRectangle? GetPresentationTarget() =>
        presentationTargetProvider?.Invoke();

    public async Task<bool> InvokeAsync(string intentId,
        GlanceContentContext context,
        CancellationToken cancellationToken = default)
    {
        IGlanceIntent? intent;

        lock (synchronization)
        {
            intents.TryGetValue(intentId, out intent);
        }

        if (intent is null ||
            !intent.CanHandle(context.Kind) ||
            !modulePreferences.GetActiveComponents().Any(component => string.Equals(component.Id, intent.Descriptor.TargetComponentId, StringComparison.OrdinalIgnoreCase)))
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

    public void SetPresentationTargetProvider(Func<GlanceScreenRectangle?>? provider) =>
        presentationTargetProvider = provider;
}
