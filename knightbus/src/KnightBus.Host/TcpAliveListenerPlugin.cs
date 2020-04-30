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
        private Task _listenerTask;

        public TcpAliveListenerPlugin(IHostConfiguration hostConfiguration, int port)
        {
            _log = hostConfiguration.Log;
            _port = port;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var listener = new TcpListener(IPAddress.Any, _port);

            _log.Information($"Starting tcp listener on port {_port}");
            await Task.Run(() => listener.Start(), cancellationToken);
            _log.Information("Tcp listener started");

            _listenerTask = Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _log.Debug("Waiting for a connection...");
                        using (var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                        {
                            _log.Debug("Received connection.");

                            var stream = client.GetStream();
                            var msg = System.Text.Encoding.ASCII.GetBytes(DateTimeOffset.UtcNow.ToString());
                            stream.Write(msg, 0, msg.Length);

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
