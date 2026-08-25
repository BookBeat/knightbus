#nullable enable
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

    /// <summary>
    /// <remarks>Should be left blank/null if using managed identity</remarks>
    /// </summary>
    public string? ConnectionString { get; set; }
    public string? FullyQualifiedNamespace { get; set; }
    public TokenCredential? Credential { get; set; }
    public ServiceBusCreationOptions DefaultCreationOptions { get; set; } =
        new ServiceBusCreationOptions();
}

public static class ServiceBusClientFactory
{
    public static ServiceBusClient CreateServiceBusClient(IServiceBusConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            return new ServiceBusClient(configuration.ConnectionString);
        }

        if (
            !string.IsNullOrWhiteSpace(configuration.FullyQualifiedNamespace)
            && configuration.Credential is not null
        )
        {
            return new ServiceBusClient(
                configuration.FullyQualifiedNamespace,
                configuration.Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(ServiceBusConfiguration)} requires either a {nameof(configuration.ConnectionString)} or a {nameof(configuration.FullyQualifiedNamespace)} with a {nameof(configuration.Credential)}."
        );
    }

    public static ServiceBusAdministrationClient CreateServiceBusAdministrationClient(
        IServiceBusConfiguration configuration
    )
    {
        if (!string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            return new ServiceBusAdministrationClient(configuration.ConnectionString);
        }

        if (
            !string.IsNullOrWhiteSpace(configuration.FullyQualifiedNamespace)
            && configuration.Credential is not null
        )
        {
            return new ServiceBusAdministrationClient(
                configuration.FullyQualifiedNamespace,
                configuration.Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(ServiceBusConfiguration)} requires either a {nameof(configuration.ConnectionString)} or a {nameof(configuration.FullyQualifiedNamespace)} with a {nameof(configuration.Credential)}."
        );
    }
}
