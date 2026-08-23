using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class TcpAliveListenerPlugin : IStoppablePlugin
{
    private readonly ILogger _log;
    private readonly int _port;
    private readonly CancellationTokenSource _stopTokenSource = new();
    private Task _listenerTask = Task.CompletedTask;

    public TcpAliveListenerPlugin(
        ITcpAliveListenerConfiguration configuration,
        ILogger<TcpAliveListenerPlugin> logger
    )
    {
        _log = logger;
        _port = configuration.Port;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.CompletedTask;

        var stopToken = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)
            .Token;
        _listenerTask = Task.Run(() => ListenAsync(stopToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);

        _log.LogInformation("Starting tcp listener on port {Port}", _port);
        listener.Start();
        _log.LogInformation("Tcp listener started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _log.LogDebug("Waiting for a connection...");
                using var client = await listener
                    .AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
                _log.LogDebug("Received connection");

                var stream = client.GetStream();
                var msg = System.Text.Encoding.ASCII.GetBytes(DateTimeOffset.UtcNow.ToString());
                try
                {
                    await stream.WriteAsync(msg, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _log.LogError(e, "Failed to write to stream");
                }
            }
        }
        catch (OperationCanceledException)
        {
            //The host is shutting down
        }
        catch (Exception e)
        {
            _log.LogError(e, "TcpAliveListenerPlugin crashed");
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        //Stop answering liveness probes as early in the shutdown as possible
        _stopTokenSource.Cancel();
        await _listenerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
