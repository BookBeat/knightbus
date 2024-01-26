using KnightBus.UI.Console.Providers.ServiceBus;
using KnightBus.UI.Console.Providers.StorageBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace KnightBus.UI.Console;

public static class Config
{
    private const string ConfigFile = ".knightbusconfig";

    public static IConfigurationRoot LoadConfig()
    {
        var configRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configPath = Path.Combine(configRoot, ConfigFile);
        if (!Path.Exists(configPath))
        {
            using StreamWriter sw = File.CreateText(configPath);
            sw.WriteLine("; config goes here");
            sw.WriteLine("[configversion]");
            sw.WriteLine("version = 1");
            sw.WriteLine($"[{ServiceBusConnectionConfig.SectionName}]");
            sw.WriteLine("connectionString = ");
            sw.WriteLine($"[{StorageConnectionConfig.SectionName}]");
            sw.WriteLine($"connectionString = ");
        }

        var configurationRoot = new ConfigurationBuilder()
            .SetFileProvider(new PhysicalFileProvider(configRoot, ExclusionFilters.None))
            .AddIniFile(ConfigFile, optional: true)
            .Build();

        return configurationRoot;
    }
}

public interface IConnectionConfig
{
    public static abstract string SectionName { get; }
    public string ConnectionString { get; set; }
}
