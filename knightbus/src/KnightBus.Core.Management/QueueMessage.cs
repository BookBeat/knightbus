using System;
using System.Collections.Generic;
using System.Linq;

namespace KnightBus.Core.Management;

public record QueueMessage(
    string Body,
    string Error,
    DateTimeOffset? Time,
    DateTimeOffset? ScheduledTime,
    int DeliveryCount,
    string MessageId,
    IReadOnlyDictionary<string, string> Properties,
    long? SequenceNumber = null
)
{
    public QueueMessage(
        string Body,
        string Error,
        DateTimeOffset? Time,
        DateTimeOffset? ScheduledTime,
        int DeliveryCount,
        string MessageId,
        IReadOnlyDictionary<string, object> Properties,
        long? SequenceNumber = null
    )
        : this(
            Body,
            Error,
            Time,
            ScheduledTime,
            DeliveryCount,
            MessageId,
            Properties.ToDictionary(x => x.Key, x => x.Value.ToString()),
            SequenceNumber
        ) { }
};
