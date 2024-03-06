// See https://aka.ms/new-console-template for more information

using System.Text;
using KnightBus.Examples.Nats;
using KnightBus.Nats;
using NATS.Client;

var connectionString = "nats://10.0.0.6:4222";
var natsConfiguration = new NatsConfiguration();
natsConfiguration.ConnectionString = connectionString;
var connectionFactory = new ConnectionFactory();

var client = new NatsBus(connectionFactory, natsConfiguration, null);

for (var i = 0; i < 1; i++)
{
    var response =
        client.RequestStream<SampleNatsRequest, SampleNatsReply>(new SampleNatsRequest { Message = $"Hello from command {i}" });
    foreach (var reply in response)
    {
        Console.WriteLine(reply?.Reply);
    }
}

for (var i = 0; i < 50; i++)
{
    await client.Publish(new SampleNatsEvent()
    {
        Message = i.ToString()
    }, CancellationToken.None);
    var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));
    stream.Position = 0;
    await client.Send(new SampleNatsCommand {Message = i.ToString()});
}

Console.WriteLine("Done");
