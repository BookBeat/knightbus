using KnightBus.Messages;

namespace KnightBus.PostgreSql;

public class DictionaryMessage : Dictionary<string, object>, IMessage;
