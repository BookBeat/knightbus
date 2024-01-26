namespace KnightBus.UI.Console.Providers.ServiceBus;

public class ServiceBusConnectionConfig : IConnectionConfig
{
    public static string SectionName => "servicebus";
    public string ConnectionString { get; set; }
}
