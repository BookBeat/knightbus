using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Host.Tests.Unit.Processors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class StandardDependencyInjectionTests
    {
        [Test]
        public void Should_register_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            //act
            provider.RegisterProcessor(new SingleCommandProcessor(Mock.Of<ICountable>()));
            //assert
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Count().Should().Be(1);
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).FirstOrDefault().Should().Be(typeof(SingleCommandProcessor));
        }

        [Test]
        public void Should_get_registered_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            var processor = new SingleCommandProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetInstance<IProcessMessage<TestCommand, Task>>(typeof(IProcessCommand<TestCommand, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
        }

        [Test]
        public void Should_register_multi_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            //act
            provider.RegisterProcessor(new MultipleCommandProcessor(Mock.Of<ICountable>()));
            //assert
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Count().Should().Be(1);
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Should().Contain(x => x == typeof(MultipleCommandProcessor));
        }
        [Test]
        public void Should_get_registered_multi_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            var processor = new MultipleCommandProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetInstance<IProcessMessage<TestCommandOne, Task>>(typeof(IProcessCommand<TestCommandOne, TestTopicSettings>));
            var processorFoundTwo = provider.GetInstance<IProcessMessage<TestCommandTwo, Task>>(typeof(IProcessCommand<TestCommandTwo, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
            processorFoundTwo.Should().Be(processor);
        }
        [Test]
        public void Should_register_event_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            //act
            provider.RegisterProcessor(new EventProcessor(Mock.Of<ICountable>()));
            //assert
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Count().Should().Be(1);
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Should().Contain(x => x == typeof(EventProcessor));
        }
        [Test]
        public void Should_get_registered_event_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            var processor = new EventProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetInstance<IProcessMessage<TestEvent, Task>>(typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
        }
        [Test]
        public void Should_register_request_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            //act
            provider.RegisterProcessor(new RequestProcessor(Mock.Of<ICountable>()));
            //assert
            var r = provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>));
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Count().Should().Be(1);
            provider.GetOpenGenericRegistrations(typeof(IProcessMessage<,>)).Should().Contain(x => x == typeof(RequestProcessor));
        }
        [Test]
        public void Should_get_registered_request_processor()
        {
            //arrange
            var provider = new StandardDependecyInjection();
            var processor = new RequestProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetInstance<IProcessMessage<TestRequest,Task<TestResponse>>>(typeof(IProcessRequest<TestRequest, TestResponse, TestMessageSettings>));
            //assert
            processorFound.Should().Be(processor);
        }
    }
}