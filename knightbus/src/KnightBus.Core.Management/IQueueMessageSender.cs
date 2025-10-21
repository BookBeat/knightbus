using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Management;

public interface IQueueMessageSender
{
    /// <summary>
    /// Sends a message to the queue
    /// </summary>
    Task SendMessage(string path, string jsonBody, CancellationToken cancellationToken);
    Task SendMessages(
        string path,
        IEnumerable<string> jsonBodies,
        CancellationToken cancellationToken
    );
}
