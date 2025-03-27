using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class TcpAliveListenerPlugin : IPlugin
{
    private readonly ILogger _log;
    private readonly int _port;

    // ReSharper disable once NotAccessedField.Local
#pragma warning disable IDE0052 // Remove unread private members
    private Task _listenerTask;
#pragma warning restore IDE0052 // Remove unread private members

    public TcpAliveListenerPlugin(
        ITcpAliveListenerConfiguration configuration,
        ILogger<TcpAliveListenerPlugin> logger
    )
    {
        _log = logger;
        _port = configuration.Port;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        _listenerTask = await Task.Factory.StartNew(
            async () =>
            {
                var listener = new TcpListener(IPAddress.Any, _port);

                _log.LogInformation($"Starting tcp listener on port {_port}");
                await Task.Run(() => listener.Start(), cancellationToken);
                _log.LogInformation("Tcp listener started");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _log.LogDebug("Waiting for a connection...");
                        using (
                            var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false)
                        )
                        {
                            _log.LogDebug("Received connection");

                            var stream = client.GetStream();
                            var msg = System.Text.Encoding.ASCII.GetBytes(
                                DateTimeOffset.UtcNow.ToString()
                            );
                            try
                            {
                                await stream
                                    .WriteAsync(msg, 0, msg.Length, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                            catch (Exception e) when (!(e is OperationCanceledException))
                            {
                                _log.LogError(e, "Failed to write to stream");
                            }

                            client.Close();
                        }
                    }
                }
                catch (Exception e) when (!(e is TaskCanceledException))
                {
                    _log.LogError(e, "TcpAliveListenerPlugin crashed");
                }
                finally
                {
                    listener.Stop();
                }
            },
            cancellationToken
        );
    }
}
