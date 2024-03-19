using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Exposes functionality for extending message lock durations
/// </summary>
public interface IMessageLockHandler<T> where T : class, IMessage
{
    Task SetLockDuration(TimeSpan timeout, CancellationToken cancellationToken);
}
