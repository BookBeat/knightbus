using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class SemaphoreQueueTests
    {
        [Test]
        public async Task Should_execute_in_correct_order()
        {
            //arrange
            var semaphore = new SemaphoreQueue(1);
            var numbers = new int[5000];
            var tasks = new List<Task>();

            //act
            for (var i = 0; i < 5000; i++)
            {
                var intCopy = i;
                tasks.Add(semaphore.WaitAsync(CancellationToken.None).ContinueWith(task =>
                {
                    semaphore.Release();
                    return numbers[intCopy] = intCopy;
                }));
            }

            await Task.WhenAll(tasks);
            //assert
            for (var i = 0; i < 5000; i++) numbers[i].Should().Be(i);
        }

        [Test]
        public async Task Should_wait()
        {
            //arrange
            var semaphore = new SemaphoreQueue(1);
            var countable = new Mock<ICountable>();
            //act
#pragma warning disable 4014
            semaphore.WaitAsync(CancellationToken.None).ContinueWith(task => countable.Object.Count());
            semaphore.WaitAsync(CancellationToken.None).ContinueWith(task => countable.Object.Count());
            semaphore.WaitAsync(CancellationToken.None).ContinueWith(task => countable.Object.Count());
#pragma warning restore 4014
            await Task.Delay(500);
            //assert
            countable.Verify(x => x.Count(), Times.Once);
            semaphore.Release();
        }

        [Test]
        public async Task Should_cancel()
        {
            //arrange
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            
            var semaphore = new SemaphoreQueue(1);

            tokenSource.Cancel();

            await semaphore
                .Awaiting(x=>  x.WaitAsync(token))
                .Should()
                .ThrowAsync<OperationCanceledException>();

            //assert
            semaphore.CurrentCount.Should().Be(1);
        }
    }
}