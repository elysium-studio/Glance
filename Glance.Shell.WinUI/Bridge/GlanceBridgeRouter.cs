using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceBridgeRouter :
    IDisposable
{
    private readonly List<GlanceBridgeConnection> connections = [];
    private readonly List<IGlanceApplicationMessageHandler> handlers = [];
    private readonly ILogger<GlanceBridgeRouter> logger;
    private readonly ModulePreferenceService preferences;
    private readonly object synchronization = new();

    public GlanceBridgeRouter(ModulePreferenceService preferences, ILogger<GlanceBridgeRouter> logger)
    {
        this.preferences = preferences;
        this.logger = logger;
        preferences.PreferencesChanged += HandlePreferencesChanged;
    }

    public void AddHandlers(IEnumerable<IGlanceApplicationMessageHandler> values)
    {
        IGlanceApplicationMessageHandler[] additions = [.. values];

        lock (synchronization)
        {
            foreach (IGlanceApplicationMessageHandler handler in additions)
            {
                if (handlers.Any(existing =>
                    string.Equals(existing.ApplicationId, handler.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    existing.Capabilities.Intersect(handler.Capabilities, StringComparer.OrdinalIgnoreCase).Any()))
                {
                    throw new InvalidOperationException($"Application bridge capabilities for '{handler.ApplicationId}' are already registered.");
                }
            }

            handlers.AddRange(additions);
        }

        BroadcastCapabilities();
    }

    public async ValueTask AddConnectionAsync(GlanceBridgeConnection connection, CancellationToken cancellationToken)
    {
        IGlanceApplicationMessageHandler[] targets;

        lock (synchronization)
        {
            connections.Add(connection);
            targets = [.. handlers.Where(handler => string.Equals(handler.ApplicationId, connection.ApplicationId, StringComparison.OrdinalIgnoreCase))];
        }

        foreach (IGlanceApplicationMessageHandler handler in targets)
        {
            await InvokeAsync(() => handler.ConnectedAsync(connection, cancellationToken), handler);
        }
    }

    public async ValueTask RemoveConnectionAsync(GlanceBridgeConnection connection, CancellationToken cancellationToken)
    {
        IGlanceApplicationMessageHandler[] targets;

        lock (synchronization)
        {
            connections.Remove(connection);
            targets = [.. handlers.Where(handler => string.Equals(handler.ApplicationId, connection.ApplicationId, StringComparison.OrdinalIgnoreCase))];
        }

        foreach (IGlanceApplicationMessageHandler handler in targets)
        {
            await InvokeAsync(() => handler.DisconnectedAsync(connection, cancellationToken), handler);
        }
    }

    public IReadOnlyCollection<string> GetCapabilities(string applicationId)
    {
        lock (synchronization)
        {
            return (string[])[.. handlers
                .Where(handler => string.Equals(handler.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) && preferences.IsEnabled(handler.ComponentId))
                .SelectMany(handler => handler.Capabilities)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
    }

    public async ValueTask RouteAsync(GlanceBridgeConnection connection, GlanceBridgeWireMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Capability) || string.IsNullOrWhiteSpace(message.Topic) || message.Payload.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        IGlanceApplicationMessageHandler[] targets;

        lock (synchronization)
        {
            targets = [.. handlers
                .Where(handler =>
                    string.Equals(handler.ApplicationId, connection.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    preferences.IsEnabled(handler.ComponentId) &&
                    handler.Capabilities.Contains(message.Capability, StringComparer.OrdinalIgnoreCase))];
        }

        GlanceApplicationMessage applicationMessage = new(message.Capability, message.Topic, message.Payload);

        foreach (IGlanceApplicationMessageHandler handler in targets)
        {
            await InvokeAsync(() => handler.HandleAsync(applicationMessage, connection, cancellationToken), handler);
        }
    }

    public void Dispose()
    {
        preferences.PreferencesChanged -= HandlePreferencesChanged;
    }

    private void HandlePreferencesChanged(object? sender, EventArgs args) => BroadcastCapabilities();

    private void BroadcastCapabilities()
    {
        GlanceBridgeConnection[] snapshot;

        lock (synchronization)
        {
            snapshot = [.. connections];
        }

        foreach (GlanceBridgeConnection connection in snapshot)
        {
            _ = SendCapabilitiesAsync(connection);
        }
    }

    private async Task SendCapabilitiesAsync(GlanceBridgeConnection connection)
    {
        try
        {
            await connection.SendCapabilitiesAsync(GetCapabilities(connection.ApplicationId));
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Failed to publish Glance bridge capabilities to {ApplicationId}", connection.ApplicationId);
        }
    }

    private async ValueTask InvokeAsync(Func<ValueTask> action, IGlanceApplicationMessageHandler handler)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Glance application bridge handler for {ApplicationId} failed", handler.ApplicationId);
        }
    }
}
