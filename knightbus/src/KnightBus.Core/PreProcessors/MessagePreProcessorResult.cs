using System.Collections.Generic;

namespace KnightBus.Core.PreProcessors;

public class MessagePreProcessorResult
{
    public static readonly MessagePreProcessorResult Continue = new(
        false,
        new Dictionary<string, object>()
    );

    public static MessagePreProcessorResult Abort() => new(true, new Dictionary<string, object>());

    public static MessagePreProcessorResult WithProperties(
        IDictionary<string, object> properties
    ) => new(false, properties);

    private MessagePreProcessorResult(bool shouldAbort, IDictionary<string, object> properties)
    {
        ShouldAbort = shouldAbort;
        Properties = properties;
    }

    public bool ShouldAbort { get; }
    public IDictionary<string, object> Properties { get; }
}
