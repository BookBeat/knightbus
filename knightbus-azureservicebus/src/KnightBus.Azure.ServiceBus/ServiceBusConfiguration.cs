using System;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Azure.ServiceBus;

public class ServiceBusConfiguration : IServiceBusConfiguration
{
    public ServiceBusConfiguration(string connectionString)
        : this()
    {
        ConnectionString = connectionString;
    }

    public ServiceBusConfiguration(string fullyQualifiedNamespace, TokenCredential credential)
        : this()
    {
        FullyQualifiedNamespace = fullyQualifiedNamespace;
        Credential = credential;
    }

    public ServiceBusConfiguration() { }

    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
    public string ConnectionString { get; set; }
    public string FullyQualifiedNamespace { get; set; }
    public TokenCredential Credential { get; set; }
    public ServiceBusCreationOptions DefaultCreationOptions { get; set; } =
        new ServiceBusCreationOptions();

    public ServiceBusClient CreateServiceBusClient()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return new ServiceBusClient(ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(FullyQualifiedNamespace) && Credential is not null)
        {
            return new ServiceBusClient(FullyQualifiedNamespace, Credential);
        }

        throw new InvalidOperationException(
            $"{nameof(ServiceBusConfiguration)} requires either a {nameof(ConnectionString)} or a {nameof(FullyQualifiedNamespace)} with a {nameof(Credential)}."
        );
    }

    public ServiceBusAdministrationClient CreateServiceBusAdministrationClient()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return new ServiceBusAdministrationClient(ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(FullyQualifiedNamespace) && Credential is not null)
        {
            return new ServiceBusAdministrationClient(FullyQualifiedNamespace, Credential);
        }

        throw new InvalidOperationException(
            $"{nameof(ServiceBusConfiguration)} requires either a {nameof(ConnectionString)} or a {nameof(FullyQualifiedNamespace)} with a {nameof(Credential)}."
        );
    }
}
