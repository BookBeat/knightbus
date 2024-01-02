namespace KnightBus.Core;

public interface IMessagePropertiesProvider
{
    string Get(string key);
    void Set(string key, string value);
}
