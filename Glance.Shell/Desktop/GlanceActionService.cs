using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System.Text.Json;

namespace Glance.Shell;

public sealed class GlanceActionService(ModulePreferenceService modulePreferences,
    IDispatcher dispatcher) :
    IGlanceActionService
{
    private readonly Dictionary<string, ActionRegistration> actions = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IDispatcher dispatcher = dispatcher;
    private readonly ModulePreferenceService modulePreferences = modulePreferences;
    private readonly object synchronization = new();

    public event EventHandler<GlanceActionInvokedEventArgs>? ActionInvoked;

    public event EventHandler<GlanceActionPresentationRequestedEventArgs>? PresentationRequested;

    public IReadOnlyList<GlanceActionDescriptor> GetActions()
    {
        IReadOnlyList<IGlanceComponent> components = modulePreferences.GetActiveComponents();
        HashSet<string> activeComponentIds = [with(StringComparer.OrdinalIgnoreCase), .. components.Select(component => component.Id)];
        GlanceActionDescriptor[] advertisedActions;

        lock (synchronization)
        {
            advertisedActions = [.. actions.Values
                .Select(registration => registration.Descriptor)
                .Where(descriptor => activeComponentIds.Contains(descriptor.TargetComponentId))];
        }

        return
        [
            .. components.Select(CreateShowDescriptor),
            .. advertisedActions
        ];
    }

    public GlanceActionDescriptor? GetAction(string actionId) => GetActions().FirstOrDefault(action => string.Equals(action.Id, actionId, StringComparison.OrdinalIgnoreCase));

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        GlanceActionDescriptor? descriptor = GetAction(request.ActionId);

        if (descriptor is null)
        {
            return GlanceActionResult.Unavailable("The requested action is not currently available.");
        }

        if (descriptor.Confirmation == GlanceActionConfirmation.Required && !request.IsConfirmed)
        {
            return new GlanceActionResult(GlanceActionStatus.ConfirmationRequired, "The action requires confirmation before it can run.");
        }

        ActionRegistration? registration = null;
        GlanceActionResult? validationResult;

        if (IsShowAction(descriptor))
        {
            registration = null;
        }
        else
        {
            lock (synchronization)
            {
                _ = actions.TryGetValue(descriptor.Id, out registration);
            }

            if (registration is null)
            {
                return GlanceActionResult.Unavailable("The action provider is no longer available.");
            }

            if (registration.Provider is IGlanceActionValidator validator)
            {
                try
                {
                    validationResult = await DispatchAsync(() => validator.ValidateAsync(request, cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new GlanceActionResult(GlanceActionStatus.Cancelled);
                }
                catch (Exception exception)
                {
                    return GlanceActionResult.Failed(exception.Message);
                }

                if (validationResult is not null)
                {
                    return validationResult;
                }
            }
        }

        validationResult = ValidateArguments(descriptor, request.Arguments);

        if (validationResult is not null)
        {
            return validationResult;
        }

        if (registration is not null && !IsProviderAvailable(registration))
        {
            return GlanceActionResult.Unavailable("That action is not available in the module's current state.");
        }

        if (descriptor.Presentation != GlanceActionPresentation.None)
        {
            PresentationRequested?.Invoke(this, new GlanceActionPresentationRequestedEventArgs(descriptor.TargetComponentId, descriptor.Presentation));
        }

        GlanceActionResult result;

        if (registration is null)
        {
            result = GlanceActionResult.Success();
        }
        else
        {
            try
            {
                result = await DispatchAsync(() => registration.Provider.InvokeAsync(request, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new GlanceActionResult(GlanceActionStatus.Cancelled);
            }
            catch (Exception exception)
            {
                return GlanceActionResult.Failed(exception.Message);
            }
        }

        if (result.Succeeded)
        {
            ActionInvoked?.Invoke(this, new GlanceActionInvokedEventArgs(descriptor.TargetComponentId, descriptor.Presentation, result));
        }

        return result;
    }

    public void Register(IEnumerable<IGlanceActionProvider> providers)
    {
        foreach (IGlanceActionProvider provider in providers)
        {
            GlanceActionDescriptor[] descriptors = [.. provider.GetActions()];

            if (descriptors.Any(descriptor => string.IsNullOrWhiteSpace(descriptor.Id) || string.IsNullOrWhiteSpace(descriptor.TargetComponentId)) ||
                descriptors.Any(descriptor => descriptor.Id.EndsWith(".Show", StringComparison.OrdinalIgnoreCase)) ||
                descriptors.Select(descriptor => descriptor.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != descriptors.Length)
            {
                throw new InvalidOperationException("A Glance action provider must advertise unique, non-empty action and component identifiers, and cannot use the reserved '.Show' action suffix.");
            }

            lock (synchronization)
            {
                string? duplicateId = descriptors.Select(descriptor => descriptor.Id).FirstOrDefault(actions.ContainsKey);

                if (duplicateId is not null)
                {
                    throw new InvalidOperationException($"A Glance action with the identifier '{duplicateId}' is already registered.");
                }

                foreach (GlanceActionDescriptor descriptor in descriptors)
                {
                    actions.Add(descriptor.Id, new ActionRegistration(descriptor, provider));
                }
            }
        }
    }

    public void Unregister(IEnumerable<IGlanceActionProvider> providers)
    {
        HashSet<IGlanceActionProvider> removals = [.. providers];

        lock (synchronization)
        {
            foreach (string id in actions.Where(item => removals.Contains(item.Value.Provider)).Select(item => item.Key).ToArray())
            {
                _ = actions.Remove(id);
            }
        }
    }

    private Task<T> DispatchAsync<T>(Func<Task<T>> action)
    {
        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Dispatch(async () =>
        {
            try
            {
                completion.SetResult(await action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return completion.Task;
    }

    private static GlanceActionDescriptor CreateShowDescriptor(IGlanceComponent component) => new($"{component.Id}.Show",
            component.Id,
            $"Show {component.DisplayName}",
            $"Bring {component.DisplayName} into view in Glance.",
            GlanceActionPresentation.Expanded);

    private static bool IsShowAction(GlanceActionDescriptor descriptor) => descriptor.Id.EndsWith(".Show", StringComparison.OrdinalIgnoreCase) && descriptor.Parameters.Count == 0;

    private static bool IsProviderAvailable(ActionRegistration registration)
    {
        try
        {
            return registration.Provider.IsAvailable(registration.Descriptor.Id);
        }
        catch
        {
            return false;
        }
    }

    private static GlanceActionResult? ValidateArguments(GlanceActionDescriptor descriptor,
        JsonElement arguments)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return descriptor.Parameters.Any(parameter => parameter.IsRequired)
                ? GlanceActionResult.InvalidArguments("One or more required arguments are missing.")
                : null;
        }

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return GlanceActionResult.InvalidArguments("Action arguments must be supplied as a JSON object.");
        }

        Dictionary<string, JsonElement> suppliedArguments = arguments.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
        HashSet<string> knownParameters = [with(StringComparer.OrdinalIgnoreCase), .. descriptor.Parameters.Select(parameter => parameter.Name)];
        string? unknownArgument = suppliedArguments.Keys.FirstOrDefault(name => !knownParameters.Contains(name));

        if (unknownArgument is not null)
        {
            return GlanceActionResult.InvalidArguments($"The argument '{unknownArgument}' is not supported by this action.");
        }

        foreach (GlanceActionParameterDescriptor parameter in descriptor.Parameters)
        {
            if (!suppliedArguments.TryGetValue(parameter.Name, out JsonElement value))
            {
                if (parameter.IsRequired)
                {
                    return GlanceActionResult.InvalidArguments($"The required argument '{parameter.Name}' is missing.");
                }

                continue;
            }

            if (!IsValidType(parameter.Type, value))
            {
                return GlanceActionResult.InvalidArguments($"The argument '{parameter.Name}' has an invalid value type.");
            }

            if (parameter.AllowedValues is { Count: > 0 } allowedValues &&
                value.ValueKind == JsonValueKind.String &&
                !allowedValues.Contains(value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                return GlanceActionResult.InvalidArguments($"The argument '{parameter.Name}' is not one of the supported values.");
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out double number) &&
                ((parameter.Minimum is double minimum && number < minimum) || (parameter.Maximum is double maximum && number > maximum)))
            {
                return GlanceActionResult.InvalidArguments($"The argument '{parameter.Name}' is outside the supported range.");
            }
        }

        return null;
    }

    private static bool IsValidType(GlanceActionParameterType type,
        JsonElement value) => type switch
        {
            GlanceActionParameterType.String => value.ValueKind == JsonValueKind.String,
            GlanceActionParameterType.Number => value.ValueKind == JsonValueKind.Number,
            GlanceActionParameterType.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            GlanceActionParameterType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };

    private sealed record ActionRegistration(GlanceActionDescriptor Descriptor,
        IGlanceActionProvider Provider);
}
