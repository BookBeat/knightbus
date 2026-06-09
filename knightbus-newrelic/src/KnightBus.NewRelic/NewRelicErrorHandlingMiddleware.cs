using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace KnightBus.NewRelicMiddleware;

public class NewRelicErrorHandlingMiddleware : ErrorHandlingMiddleware
{
    public NewRelicErrorHandlingMiddleware(ILogger<NewRelicErrorHandlingMiddleware> log)
        : base(log) { }

    [Transaction]
    public override async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
    {
        NewRelic.Api.Agent.NewRelic.SetTransactionName("Message", typeof(T).FullName);
        await base.ProcessAsync(messageStateHandler, pipelineInformation, next, cancellationToken)
            .ConfigureAwait(false);
    }

    protected override Task OnProcessingError(Exception e)
    {
        NewRelic.Api.Agent.NewRelic.NoticeError(e);
        return Task.CompletedTask;
    }
}
