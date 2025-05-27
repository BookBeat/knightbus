using System.Collections.Concurrent;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos.Messages;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Cosmos.Tests.Integration;

class ProcessedTracker()
{
    //Uses message strings as id - therefore all message strings need to be unique
    public static ConcurrentDictionary<string, int> processed { get; set; } =
        new ConcurrentDictionary<string, int>();

    public static void Increment(string key)
    {
        processed.AddOrUpdate(key, 1, (_, val) => val + 1);
    }

    //Gets the number of events processed by all subscribers
    public static int Processed(IEnumerable<string> messageStrings, int deliveriesPerMessage = 1)
    {
        int processed = 0;
        foreach (var id in messageStrings)
        {
            if (
                ProcessedTracker.processed.TryGetValue(id, out var value)
                && value >= deliveriesPerMessage
            )
            {
                processed++;
            }
        }
        return processed;
    }

    public static async Task WaitForProcessedMessagesAsync(
        IEnumerable<string> messageStrings,
        int numMessages,
        int deliveriesPerMessage = 1,
        int TimeOutSeconds = 10
    )
    {
        var startTime = DateTime.UtcNow;
        while (
            Processed(messageStrings, deliveriesPerMessage) < numMessages * deliveriesPerMessage
            && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(TimeOutSeconds)
        )
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
