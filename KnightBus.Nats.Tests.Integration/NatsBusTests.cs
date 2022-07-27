using FluentAssertions;
using NATS.Client;

namespace KnightBus.Nats.Tests.Integration;

public class NatsBusTests
{
    private INatsBusConfiguration _configuration = null!;
    private IConnection _connection = null!;
    private NatsBus _target = null!;

    [SetUp]
    public void Setup()
    {
        _configuration = new NatsBusConfiguration("");
        _connection = new ConnectionFactory().CreateConnection();
        
        _target = new NatsBus(_connection, _configuration);
    }
    
    [Test]
    public async Task RequestAsync_WhenResponders_ReturnsResponse()
    {
        //Arrange
        _connection.SubscribeAsync(new TestNatsRequestMapping().QueueName, (_, args) =>
        {
            _connection.Publish(new Msg(args.Message.Reply, _configuration.MessageSerializer.Serialize(new TestNatsResponse
            {
                Payload = "response"
            })));
            _connection.Flush();
        });
        
        //Act 
        var response = await _target.RequestAsync<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());
        
        //Assert
        response.Payload.Should().Be("response");
    }
    
    [Test]
    public async Task RequestAsync_WhenNoResponders_Throws()
    {
        await _target.Invoking(x => x.RequestAsync<TestNatsRequest, TestNatsResponse>(new TestNatsRequest()))
            .Should().ThrowAsync<NATSNoRespondersException>();
    }
    
    [Test]
    public async Task RequestAsync_WhenNoResponse_Throws()
    {
        //Arrange
        _connection.SubscribeAsync(new TestNatsRequestMapping().QueueName); //Subscribe without response
        
        //Act & Assert
        await _target.Invoking(x => x.RequestAsync<TestNatsRequest, TestNatsResponse>(new TestNatsRequest()))
            .Should().ThrowAsync<NATSTimeoutException>();
    }
}
