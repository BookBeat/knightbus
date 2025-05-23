﻿using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Azure.ServiceBus;

public class ServiceBusConfiguration : IServiceBusConfiguration
{
    public ServiceBusConfiguration(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public ServiceBusConfiguration() { }

    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
    public string ConnectionString { get; set; }
    public ServiceBusCreationOptions DefaultCreationOptions { get; set; } =
        new ServiceBusCreationOptions();
}
