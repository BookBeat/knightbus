using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using KnightBus.Core.Singleton;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class SingletonAssignmentTests
{
    private static List<SingletonHostInfo> Hosts(
        int count,
        IReadOnlyDictionary<string, int> locks,
        Random random
    ) =>
        Enumerable
            .Range(0, count)
            .Select(_ => new SingletonHostInfo($"pod-{random.NextInt64():x}", locks))
            .ToList();

    private static Dictionary<string, int> Locks(int light, params int[] heavy)
    {
        var locks = Enumerable.Range(0, light).ToDictionary(i => $"Light{i:D2}", _ => 1);
        for (var i = 0; i < heavy.Length; i++)
            locks[$"Heavy{i}"] = heavy[i];
        return locks;
    }

    private static Dictionary<string, long> LoadPerHost(
        IEnumerable<SingletonHostInfo> hosts,
        IReadOnlyDictionary<string, string> assignment,
        IReadOnlyDictionary<string, int> weights
    )
    {
        var load = hosts.ToDictionary(h => h.HostId, _ => 0L);
        foreach (var (lockId, host) in assignment)
            load[host] += weights[lockId];
        return load;
    }

    [Test]
    public void Should_assign_nothing_without_hosts()
    {
        SingletonAssignment.Compute([]).Should().BeEmpty();
    }

    [Test]
    public void Should_give_a_single_host_every_lock()
    {
        var hosts = Hosts(1, Locks(5, 10), new Random(1));

        var assignment = SingletonAssignment.Compute(hosts);

        assignment.Values.Distinct().Should().ContainSingle().Which.Should().Be(hosts[0].HostId);
        assignment.Should().HaveCount(6);
    }

    [Test]
    public void Should_assign_every_lock_exactly_once_to_a_host_that_can_run_it()
    {
        var random = new Random(2);
        for (var run = 0; run < 200; run++)
        {
            //arrange: hosts that only run a random subset of the locks, like mixed versions in a deploy
            var all = Locks(random.Next(1, 25), random.Next(1, 4) == 1 ? [8] : []);
            var hosts = Enumerable
                .Range(0, random.Next(1, 15))
                .Select(i => new SingletonHostInfo(
                    $"pod-{random.NextInt64():x}",
                    all.Where(_ => random.NextDouble() < 0.7).ToDictionary(x => x.Key, x => x.Value)
                ))
                .ToList();

            //act
            var assignment = SingletonAssignment.Compute(hosts);

            //assert
            var runnable = hosts.SelectMany(h => h.Locks.Keys).Distinct().ToList();
            assignment.Keys.Should().BeEquivalentTo(runnable);
            foreach (var (lockId, host) in assignment)
                hosts.Single(h => h.HostId == host).Locks.Should().ContainKey(lockId);
        }
    }

    [Test]
    public void Should_give_the_same_answer_whatever_order_the_hosts_are_listed_in()
    {
        var random = new Random(3);
        var locks = Locks(17, 10, 10);
        var hosts = Hosts(11, locks, random);
        var expected = SingletonAssignment.Compute(hosts);

        for (var i = 0; i < 50; i++)
        {
            var shuffled = hosts.OrderBy(_ => random.Next()).ToList();
            SingletonAssignment.Compute(shuffled).Should().BeEquivalentTo(expected);
        }
    }

    [Test]
    public void Should_use_the_largest_weight_when_hosts_advertise_different_ones()
    {
        var old = new SingletonHostInfo(
            "old",
            new Dictionary<string, int> { ["Batch"] = 1, ["Other"] = 1 }
        );
        var updated = new SingletonHostInfo(
            "new",
            new Dictionary<string, int> { ["Batch"] = 10, ["Other"] = 1 }
        );

        var one = SingletonAssignment.Compute([old, updated]);
        var other = SingletonAssignment.Compute([updated, old]);

        one.Should().BeEquivalentTo(other);
        //the heavy lock fills a host, so the light one goes to the other
        one["Batch"].Should().NotBe(one["Other"]);
    }

    [Test]
    public void Should_keep_heavy_locks_on_different_hosts()
    {
        //two heavy processors among many light ones, on 11 instances
        var random = new Random(4);
        var locks = Locks(17, 10, 10);
        for (var run = 0; run < 300; run++)
        {
            var assignment = SingletonAssignment.Compute(Hosts(11, locks, random));

            assignment["Heavy0"].Should().NotBe(assignment["Heavy1"]);
        }
    }

    [Test]
    public void Should_not_put_light_locks_on_a_host_that_already_holds_a_heavy_one()
    {
        var random = new Random(5);
        var locks = Locks(17, 10, 10);
        for (var run = 0; run < 300; run++)
        {
            var assignment = SingletonAssignment.Compute(Hosts(11, locks, random));

            var heavyHosts = new[] { assignment["Heavy0"], assignment["Heavy1"] };
            assignment
                .Where(x => x.Key.StartsWith("Light"))
                .Select(x => x.Value)
                .Should()
                .NotContain(heavyHosts);
        }
    }

    [Test]
    public void Should_bound_the_load_of_every_host_for_equal_weights()
    {
        var random = new Random(6);
        for (var run = 0; run < 400; run++)
        {
            var hostCount = random.Next(1, 31);
            var lockCount = random.Next(1, 41);
            var locks = Locks(lockCount);
            var hosts = Hosts(hostCount, locks, random);

            var load = LoadPerHost(hosts, SingletonAssignment.Compute(hosts), locks);

            var cap = Math.Ceiling(1.25 * lockCount / hostCount);
            load.Values.Max()
                .Should()
                .BeLessThanOrEqualTo((long)cap, $"{lockCount} locks on {hostCount} hosts");
        }
    }

    [Test]
    public void Should_move_few_locks_that_do_not_have_to_move_when_a_host_leaves()
    {
        var random = new Random(7);
        var locks = Locks(17, 10, 10);
        long unneeded = 0;
        const int runs = 300;
        for (var run = 0; run < runs; run++)
        {
            var hosts = Hosts(11, locks, random);
            var before = SingletonAssignment.Compute(hosts);
            var leaving = hosts[random.Next(hosts.Count)];

            var after = SingletonAssignment.Compute(hosts.Where(h => h != leaving).ToList());

            //locks of the leaving host have to move, the others should mostly stay
            unneeded += before.Count(x => x.Value != leaving.HostId && after[x.Key] != x.Value);
        }

        //measured at about 0.8 of 19 locks per departure
        (unneeded / (double)runs / locks.Count)
            .Should()
            .BeLessThan(0.10);
    }

    [Test]
    public void Should_move_a_bounded_share_of_the_locks_when_a_host_joins()
    {
        var random = new Random(8);
        var locks = Locks(19);
        foreach (var hostCount in new[] { 2, 4, 6, 11 })
        {
            long moved = 0;
            const int runs = 300;
            for (var run = 0; run < runs; run++)
            {
                var hosts = Hosts(hostCount, locks, random);
                var before = SingletonAssignment.Compute(hosts);

                var after = SingletonAssignment.Compute(
                    hosts.Concat(Hosts(1, locks, random)).ToList()
                );

                moved += before.Count(x => after[x.Key] != x.Value);
            }

            //a joining host should end up with about 1/(N+1) of the locks; measured at up to 1.8 times that
            var ideal = locks.Count / (double)(hostCount + 1);
            (moved / (double)runs).Should().BeLessThan(2.2 * ideal, $"{hostCount} hosts + 1");
        }
    }

    [Test]
    public void Should_give_every_host_work_when_there_are_enough_locks_and_no_slack()
    {
        var random = new Random(9);
        var locks = Locks(19);
        for (var run = 0; run < 200; run++)
        {
            var hosts = Hosts(11, locks, random);

            var load = LoadPerHost(hosts, SingletonAssignment.Compute(hosts, epsilon: 0), locks);

            load.Values.Should().OnlyContain(v => v >= 1 && v <= 2);
        }
    }

    [Test]
    public void Should_reject_a_negative_epsilon()
    {
        var assign = () => SingletonAssignment.Compute([], -0.1);

        assign.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Should_score_the_same_on_every_runtime()
    {
        //if this changes, hosts running different versions would disagree about who owns a lock
        SingletonAssignment.Score("KnightBus.Lock", "pod-1").Should().Be(9586639450076303934UL);
    }
}
