namespace KnightBus.UI.Blazor;

public enum Environment
{
    Dev,
    Test,
    Prod
}

public record EnvironmentConfig(string ServiceBusConnectionString, string StorageBusConnectionString);

public class EnvironmentService
{
    private Environment _environment = Environment.Dev;
    public void Set(Environment environment)
    {
        _environment = environment;
    }

    public Environment Get()
    {
        return _environment;
    }
}

public static class ConfigurationExtensions
{
    public static EnvironmentConfig GetEnvironmentConfig(this IConfiguration configuration, Environment environment)
    {
        return new EnvironmentConfig(
            configuration.GetValue<string>($"Environments:{environment}:ServiceBus:ConnectionString")!,
            configuration.GetValue<string>($"Environments:{environment}:StorageBus:ConnectionString")!);
    }
}
