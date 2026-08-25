using KnightBus.Messages;

namespace KnightBus.PostgreSql.Management;

public class DictionaryMessage : Dictionary<string, object>, IMessage;
