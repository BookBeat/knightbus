using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Management;

public interface IQueueMessageAttachmentProvider
{
    /// <summary>
    /// Gets the attachment for the given queue and attachment id.
    /// </summary>
    /// <returns>The attachment if it exists, otherwise <see langword="null" /></returns>
    Task<QueueMessageAttachment> GetAttachment(
        string queue,
        Dictionary<string, string> messageProperties,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks if the message has an attachment.
    /// </summary>
    /// <param name="messageProperties">The properties of the message.</param>
    /// <returns><c>true</c> if an attachment exists, otherwise <c>false</c></returns>
    bool HasAttachment(Dictionary<string, string> messageProperties);
}
