using System.Collections.Generic;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public interface
        IProcessStreamRequest<TRequest, TResponse, TSettings> : IProcessMessage<TRequest, IAsyncEnumerable<TResponse>>
        where TRequest : IRequest
        where TSettings : class, IProcessingSettings, new()
    {

    }
}
