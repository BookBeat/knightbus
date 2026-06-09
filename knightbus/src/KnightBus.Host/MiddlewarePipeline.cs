using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

internal class MiddlewarePipeline
{
    private readonly IPipelineInformation _pipelineInformation;
    private readonly List<IMessageProcessorMiddleware> _middlewares =
        new List<IMessageProcessorMiddleware>();

    public MiddlewarePipeline(
        IEnumerable<IMessageProcessorMiddleware> hostMiddlewares,
        IPipelineInformation pipelineInformation,
        ILogger log
    )
    {
        _pipelineInformation = pipelineInformation;

        var processorMiddlewares = new List<IMessageProcessorMiddleware>(hostMiddlewares);

        var errorHandling = Extract<IErrorHandlingMiddleware>(processorMiddlewares);
        var scopeProvider = Extract<IMessageScopeProviderMiddleware>(processorMiddlewares);

        _middlewares.Add(errorHandling ?? new ErrorHandlingMiddleware(log));
        _middlewares.Add(
            scopeProvider ?? new MicrosoftDependencyInjectionScopedLifeStyleMiddleware()
        );
        _middlewares.Add(new DeadLetterMiddleware());
        _middlewares.AddRange(processorMiddlewares);
    }

    private static IMessageProcessorMiddleware Extract<TMiddleware>(
        List<IMessageProcessorMiddleware> middlewares
    )
        where TMiddleware : IMessageProcessorMiddleware
    {
        var middleware = middlewares.SingleOrDefault(m => m is TMiddleware);
        if (middleware != null)
            middlewares.Remove(middleware);

        return middleware;
    }

    public IMessageProcessor GetPipeline(IMessageProcessor baseProcessor)
    {
        var processors = new IMessageProcessor[_middlewares.Count + 1];
        processors[processors.Length - 1] = baseProcessor;
        for (var i = processors.Length - 2; i >= 0; i--)
        {
            processors[i] = new MiddlewareWrapper(
                _middlewares[i],
                _pipelineInformation,
                processors[i + 1]
            );
        }

        return processors[0];
    }
}
