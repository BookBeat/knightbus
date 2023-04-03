using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace KnightBus.UI.Console;

public static class Config
{
    public const string ServiceBusConnectionKey = "serviceBusConnection";
    public const string StorageConnectionKey = "storageConnection";
    private const string ConfigFile = ".knightbusconfig";

    public static IConfigurationRoot LoadConfig()
    {
        var configRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configPath = Path.Combine(configRoot, ConfigFile);
        if (!Path.Exists(configPath))
        {
            using StreamWriter sw = File.CreateText(configPath);
            sw.WriteLine("; config goes here");
            sw.WriteLine($"{ServiceBusConnectionKey} = value");
            sw.WriteLine($"{StorageConnectionKey} = value");
        }

        var configurationRoot = new ConfigurationBuilder()
            .SetFileProvider(new PhysicalFileProvider(configRoot, ExclusionFilters.None))
            .AddIniFile(ConfigFile, optional: true)
            .Build();

        return configurationRoot;
    }
}
