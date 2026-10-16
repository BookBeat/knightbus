using System;
using System.Collections.Generic;
using AwesomeAssertions;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class SingletonOptionsTests
{
    private static IEnumerable<TestCaseData> Rejected()
    {
        yield return Case(
            "zero poll interval",
            o => o.PollInterval = TimeSpan.Zero,
            "PollInterval"
        );
        yield return Case(
            "negative poll interval",
            o => o.PollInterval = TimeSpan.FromSeconds(-1),
            "PollInterval"
        );
        yield return Case("negative jitter", o => o.PollJitter = -0.01, "PollJitter");
        yield return Case("jitter above one", o => o.PollJitter = 1.01, "PollJitter");
        yield return Case("jitter not a number", o => o.PollJitter = double.NaN, "PollJitter");
        yield return Case(
            "infinite jitter",
            o => o.PollJitter = double.PositiveInfinity,
            "PollJitter"
        );
        yield return Case(
            "zero renewal interval",
            o => o.RenewalInterval = TimeSpan.Zero,
            "RenewalInterval"
        );
        yield return Case(
            "lock that expires between renewals",
            o =>
            {
                o.LockDuration = TimeSpan.FromSeconds(10);
                o.RenewalInterval = TimeSpan.FromSeconds(19);
            },
            "LockDuration*RenewalInterval"
        );
        yield return Case(
            "lock as long as the renewal interval",
            o => o.LockDuration = o.RenewalInterval,
            "LockDuration*RenewalInterval"
        );
    }

    private static TestCaseData Case(
        string name,
        Action<SingletonOptions> configure,
        string message
    ) => new TestCaseData(configure, message).SetArgDisplayNames(name);

    [TestCaseSource(nameof(Rejected))]
    public void Should_reject_options_a_lock_cannot_work_with(
        Action<SingletonOptions> configure,
        string message
    )
    {
        //act
        var configuring = () => new ServiceCollection().ConfigureSingletons(configure);

        //assert
        configuring.Should().Throw<ArgumentException>().WithMessage($"*{message}*");
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Should_accept_a_poll_jitter_at_either_end_of_its_range(double jitter)
    {
        //act
        var configuring = () =>
            new ServiceCollection().ConfigureSingletons(options => options.PollJitter = jitter);

        //assert
        configuring.Should().NotThrow();
    }

    [Test]
    public void Should_accept_the_defaults()
    {
        //act
        var configuring = () => new ServiceCollection().ConfigureSingletons(_ => { });

        //assert
        configuring.Should().NotThrow();
    }
}
