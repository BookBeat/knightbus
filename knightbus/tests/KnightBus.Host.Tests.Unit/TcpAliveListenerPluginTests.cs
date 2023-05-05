using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class TcpAliveListenerPluginTests
    {
        [Test]
        public async Task Should_RespondToPing()
        {
            //Arrange
            var target = new TcpAliveListenerPlugin(new TcpAliveListenerConfiguration(13000), Mock.Of<ILogger<TcpAliveListenerPlugin>>());
            await target.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));

            //Act
            var result = TestTcpClient.Ping("127.0.0.1", 13000);

            //Assert
            result.Should().NotBeNullOrEmpty();
        }
    }

    public static class TestTcpClient
    {
        public static string Ping(string host, int port)
        {
            var client = new TcpClient(host, port);

            var stream = client.GetStream();

            stream.Write(new byte[1], 0, 1);

            var data = new byte[256];
            var bytes = stream.Read(data, 0, data.Length);
            var responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);

            stream.Close();
            client.Close();

            return responseData;
        }
    }
}
