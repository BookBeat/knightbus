using KnightBus.Messages;

namespace KnightBus.Nats
{
    public interface INatsMessage : IMessage
    {

    }

    public interface INatsRequest<T> : IRequest, INatsMessage where T:INatsReponse
    {

    }

    public interface INatsReponse : INatsMessage
    {

    }
}