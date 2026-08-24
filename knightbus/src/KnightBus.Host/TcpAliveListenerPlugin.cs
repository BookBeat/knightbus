using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class TcpAliveListenerPlugin : IStoppablePlugin, IDisposable
{
    private readonly ILogger _log;
    private readonly int _port;
    private readonly CancellationTokenSource _stopTokenSource = new();
    private CancellationTokenSource _listenerTokenSource;
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
        if (_listenerTokenSource != null)
            throw new InvalidOperationException("The tcp alive listener is already started");

        var listenerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _stopTokenSource.Token
        );

        //Bind here rather than on the listener task, so that a port that cannot be bound
        //fails the host startup instead of silently leaving nothing listening
        var listener = new TcpListener(IPAddress.Any, _port);
        _log.LogInformation("Starting tcp listener on port {Port}", _port);
        try
        {
            listener.Start();
        }
        catch
        {
            //Leave the plugin unstarted so it can be started again
            listenerTokenSource.Dispose();
            throw;
        }
        _log.LogInformation("Tcp listener started");

        _listenerTokenSource = listenerTokenSource;
        _listenerTask = Task.Run(
            () => ListenAsync(listener, listenerTokenSource.Token),
            CancellationToken.None
        );
        return Task.CompletedTask;
    }

    private async Task ListenAsync(TcpListener listener, CancellationToken cancellationToken)
    {
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
        await _stopTokenSource.CancelAsync().ConfigureAwait(false);
        await _listenerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _stopTokenSource.Dispose();
        _listenerTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
