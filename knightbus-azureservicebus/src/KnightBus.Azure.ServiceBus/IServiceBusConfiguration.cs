using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus;

public interface IServiceBusConfiguration : ITransportConfiguration
{
    ServiceBusCreationOptions DefaultCreationOptions { get; }

    /// <summary>
    /// Used when using a <see cref="TokenCredential"/>. The fully qualified Service Bus namespace to connect to. This is likely to be similar to <c>{yournamespace}.servicebus.windows.net</c>
    /// </summary>
    string FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// The Azure managed identity credential to use for authorization. Access controls may be specified by the Service Bus namespace.
    /// </summary>
    TokenCredential Credential { get; set; }

    ServiceBusClient CreateServiceBusClient();
    ServiceBusAdministrationClient CreateServiceBusAdministrationClient();
}
