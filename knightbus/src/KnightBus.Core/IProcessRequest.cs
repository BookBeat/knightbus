using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public interface IProcessRequest<TRequest, TResponse, TSettings> :IProcessMessage<TRequest, Task<TResponse>>
        where TRequest : IRequest
        where TSettings : class, IProcessingSettings, new() 
    {
        
    }

    public interface IProcessStreamRequest<TRequest, TResponse, TSettings> : IProcessMessage<TRequest, IAsyncEnumerable<TResponse>>
        where TRequest : IRequest
        where TSettings : class, IProcessingSettings, new()
    {

    }
}