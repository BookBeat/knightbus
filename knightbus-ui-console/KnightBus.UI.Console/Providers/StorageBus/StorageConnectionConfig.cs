namespace KnightBus.UI.Console.Providers.StorageBus;

public class StorageConnectionConfig : IConnectionConfig
{
    public static string SectionName => "azurestorage";
    public string ConnectionString { get; set; }
}
