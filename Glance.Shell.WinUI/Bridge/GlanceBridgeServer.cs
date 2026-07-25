using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceBridgeServer(
    GlanceBridgeRouter router,
    ILogger<GlanceBridgeServer> logger) :
    BackgroundService
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe = new(
                GlanceBridgeProtocol.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                _ = HandleConnectionAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await pipe.DisposeAsync();
            }
            catch (Exception exception)
            {
                await pipe.DisposeAsync();
                logger.LogError(exception, "The Glance application bridge failed while accepting a connection");
            }
        }
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        GlanceBridgeConnection? connection = null;

        try
        {
            using StreamReader reader = new(pipe, Encoding.UTF8, false, leaveOpen: true);
            StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: false);
            string? helloJson = await reader.ReadLineAsync(cancellationToken);

            if (helloJson is null)
            {
                return;
            }

            GlanceBridgeWireMessage? hello = JsonSerializer.Deserialize<GlanceBridgeWireMessage>(helloJson, serializerOptions);

            if (hello is not { Kind: "hello", ProtocolVersion: GlanceBridgeProtocol.Version } || string.IsNullOrWhiteSpace(hello.ApplicationId))
            {
                return;
            }

            connection = new GlanceBridgeConnection(hello.ApplicationId, writer);
            await router.AddConnectionAsync(connection, cancellationToken);

            await connection.SendCapabilitiesAsync(router.GetCapabilities(connection.ApplicationId), cancellationToken);

            while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
            {
                string? json = await reader.ReadLineAsync(cancellationToken);

                if (json is null)
                {
                    break;
                }

                GlanceBridgeWireMessage? message = JsonSerializer.Deserialize<GlanceBridgeWireMessage>(json, serializerOptions);

                if (message is { Kind: "publish", ProtocolVersion: GlanceBridgeProtocol.Version })
                {
                    await router.RouteAsync(connection, message, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "An application sent an invalid Glance bridge message");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Glance application bridge connection failed");
        }
        finally
        {
            if (connection is not null)
            {
                await router.RemoveConnectionAsync(connection, CancellationToken.None);
                await connection.DisposeAsync();
            }
            else
            {
                await pipe.DisposeAsync();
            }
        }
    }
}
