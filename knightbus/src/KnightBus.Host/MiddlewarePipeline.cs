﻿using System.Collections.Generic;
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

        //Add default outlying middlewares
        _middlewares.Add(new ErrorHandlingMiddleware(log));

        //See if there is a IMessageScopeProviderMiddleware that needs to be placed before the other middlewares
        var processorMiddlewares = new List<IMessageProcessorMiddleware>(hostMiddlewares);
        var scopeProvider = processorMiddlewares.SingleOrDefault(scopeMiddleware =>
            scopeMiddleware is IMessageScopeProviderMiddleware
        );
        if (scopeProvider != null)
        {
            _middlewares.Add(scopeProvider);
            processorMiddlewares.Remove(scopeProvider);
        }
        else
        {
            _middlewares.Add(new MicrosoftDependencyInjectionScopedLifeStyleMiddleware());
        }

        _middlewares.Add(new DeadLetterMiddleware());
        //Add host-global middlewares
        _middlewares.AddRange(processorMiddlewares);
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
