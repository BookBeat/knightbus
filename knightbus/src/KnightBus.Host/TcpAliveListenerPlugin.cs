using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host
{
    internal class TcpAliveListenerPlugin : IPlugin
    {
        private readonly ILog _log;
        private readonly int _port;

        // ReSharper disable once NotAccessedField.Local
#pragma warning disable IDE0052 // Remove unread private members
        private Task _listenerTask;
#pragma warning restore IDE0052 // Remove unread private members

        public TcpAliveListenerPlugin(IHostConfiguration hostConfiguration, int port)
        {
            _log = hostConfiguration.Log;
            _port = port;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            _listenerTask = await Task.Factory.StartNew(async () =>
            {
                var listener = new TcpListener(IPAddress.Any, _port);

                _log.Information($"Starting tcp listener on port {_port}");
                await Task.Run(() => listener.Start(), cancellationToken);
                _log.Information("Tcp listener started");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _log.Debug("Waiting for a connection...");
                        using (var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                        {
                            _log.Debug("Received connection");

                            var stream = client.GetStream();
                            var msg = System.Text.Encoding.ASCII.GetBytes(DateTimeOffset.UtcNow.ToString());
                            try
                            {
                                await stream.WriteAsync(msg, 0, msg.Length, cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception e) when (!(e is OperationCanceledException))
                            {
                                _log.Error(e, "Failed to write to stream");
                            }

                            client.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e, "TcpAliveListenerPlugin crashed");
                }
                finally
                {
                    listener.Stop();
                }
            }, cancellationToken);
        }
    }
}
