using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public interface IProcessRequest<TRequest, TResponse, TSettings> :IProcessRequest<TRequest, TResponse>
        where TRequest : IRequest
        where TSettings : class, IProcessingSettings, new() 
    {
        
    }

    public interface IProcessRequest<TRequest, TResponse> where TRequest : IMessage
    {
        Task<TResponse> ProcessAsync(TRequest message, CancellationToken cancellationToken);
    }
}