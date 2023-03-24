using System;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host.Tests.Unit
{
    public class TestRequest : IRequest
    {

    }
    public class TestRequestMapping : IMessageMapping<TestRequest>
    {
        public string QueueName => "testreq";
    }

    public class TestResponse
    {

    }
    public class TestCommand : ICommand
    {
        public bool Throw { get; set; }
    }

    public class TestMessageMapping : IMessageMapping<TestCommand>
    {
        public string QueueName => "testcommand";
    }

    public class TestCommandOne : ICommand
    {
        public bool Throw { get; set; }
    }
    public class TestMessageOneMapping : IMessageMapping<TestCommandOne>
    {
        public string QueueName => "testcommand";
    }

    public class TestEvent : IEvent
    {
    }
    public class TestEventMapping : IMessageMapping<TestEvent>
    {
        public string QueueName => "testevent";
    }

    public class TestCommandTwo : ICommand
    {
    }
    public class AttachmentCommand : ICommandWithAttachment, ICommand
    {
        public string Message { get; set; }
        public IMessageAttachment Attachment { get; set; }
    }
    public class AttachmentMessageMapping : IMessageMapping<AttachmentCommand>
    {
        public string QueueName => "attachmentcommand";
    }
    public class TestCommand2Mapping : IMessageMapping<TestCommandTwo>
    {
        public string QueueName => "testcommand2";
    }
    public class SingletonCommand : ICommand
    {
    }
    public class SingletonMessageMapping : IMessageMapping<SingletonCommand>
    {
        public string QueueName => "singletoncommand";
    }

    public class TestTopicSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 2;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
        public int DeadLetterDeliveryLimit => 1;
        public int PrefetchCount { get; }
    }

    public class TestMessageSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls { get; set; } = 1;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
        public int DeadLetterDeliveryLimit { get; set; } = 1;
        public int PrefetchCount { get; set; }
    }

    public class TestSubscription : IEventSubscription<TestEvent>
    {
        public string Name => "test-sub";
    }
}
