using FluentAssertions;
using NATS.Client;
// ReSharper disable PossibleMultipleEnumeration

namespace KnightBus.Nats.Tests.Integration;

public class NatsBusTests
{
    private INatsConfiguration _configuration = null!;
    private IConnectionFactory _connectionFactory = null!;
    private IConnection _connection = null!;
    private NatsBus _target = null!;

    [SetUp]
    public void Setup()
    {
        _configuration = new NatsConfiguration();
        _connectionFactory = new ConnectionFactory();
        _connection = _connectionFactory.CreateConnection();
        
        _target = new NatsBus(_connectionFactory, _configuration);
    }

    [TearDown]
    public async Task Teardown()
    {
        await _connection.DrainAsync();
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
    
    [Test]
    public void RequestStreamAsync_WhenResponders_ReturnsResponse()
    {
        //Arrange
        var stopHeader = new MsgHeader {{MsgConstants.HeaderName, MsgConstants.Completed}};
        
        _connection.SubscribeAsync(new TestNatsRequestMapping().QueueName, (_, args) =>
        {
            _connection.Publish(new Msg(args.Message.Reply, _configuration.MessageSerializer.Serialize(new TestNatsResponse
            {
                Payload = "response"
            })));
            _connection.Publish(new Msg(args.Message.Reply, _configuration.MessageSerializer.Serialize(new TestNatsResponse
            {
                Payload = "response 2"
            })));
            _connection.Publish(new Msg(args.Message.Reply, stopHeader, Array.Empty<byte>()));
        });
        
        //Act 
        var response = _target.RequestStream<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());
        
        //Assert
        response.Should().HaveCount(2);
        response.Should().Contain(x => x.Payload == "response");
        response.Should().Contain(x => x.Payload == "response 2");
    }
    
    [Test, Ignore("Not same mechanics as Request - should it be?")]
    public void RequestStreamAsync_WhenNoResponders_Throws()
    {
        _target.Invoking(x => x.RequestStream<TestNatsRequest, TestNatsResponse>(new TestNatsRequest()))
            .Should().Throw<NATSNoRespondersException>();
    }
    
    [Test]
    public void RequestStreamAsync_WhenNoResponse_Throws()
    {
        //Arrange
        var response = _target.RequestStream<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());
        
        //Act & Assert
        response.Invoking(x => x.ToList()).Should().Throw<NATSTimeoutException>(); // Force enumeration
    }
}
