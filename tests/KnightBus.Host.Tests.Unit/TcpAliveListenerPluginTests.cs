using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class TcpAliveListenerPluginTests
{
    [Test]
    public async Task Should_RespondToPing()
    {
        //Arrange
        var target = new TcpAliveListenerPlugin(
            new TcpAliveListenerConfiguration(13000),
            Mock.Of<ILogger<TcpAliveListenerPlugin>>()
        );
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));

        //Act
        var result = TestTcpClient.Ping("127.0.0.1", 13000);

        //Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Should_fail_to_start_when_the_port_cannot_be_bound()
    {
        //Arrange: something else already holds the port
        var blocker = new TcpListener(IPAddress.Any, 13002);
        blocker.Start();
        try
        {
            var target = new TcpAliveListenerPlugin(
                new TcpAliveListenerConfiguration(13002),
                Mock.Of<ILogger<TcpAliveListenerPlugin>>()
            );

            //Act & assert: starting must fail loudly rather than leave nothing listening
            await target
                .Awaiting(x => x.StartAsync(CancellationToken.None))
                .Should()
                .ThrowAsync<SocketException>();
        }
        finally
        {
            blocker.Stop();
        }
    }

    [Test]
    public async Task Should_stop_answering_when_stopped()
    {
        //Arrange
        var target = new TcpAliveListenerPlugin(
            new TcpAliveListenerConfiguration(13001),
            Mock.Of<ILogger<TcpAliveListenerPlugin>>()
        );
        await target.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));
        TestTcpClient.Ping("127.0.0.1", 13001).Should().NotBeNullOrEmpty();

        //Act
        await target.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        //Assert
        var ping = () => TestTcpClient.Ping("127.0.0.1", 13001);
        ping.Should().Throw<SocketException>("the listener must go dark as soon as it is stopped");
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
