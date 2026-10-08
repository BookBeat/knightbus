using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnightBus.Core.Singleton;

/// <summary>
/// A live host and the singleton locks it can run, with the weight of each lock.
/// </summary>
public sealed record SingletonHostInfo(string HostId, IReadOnlyDictionary<string, int> Locks);

/// <summary>
/// Decides which host should hold which singleton lock. Every host computes the same answer from
/// the same inputs, so no coordinator is needed. The answer is advisory: the lock itself still
/// decides who processes.
/// </summary>
/// <remarks>
/// Rendezvous hashing ranks the hosts for each lock, which keeps a lock on the same host when
/// others join or leave. A load cap on top of the ranking keeps heavy locks apart and stops one host
/// ending up with a disproportionate share.
/// </remarks>
public static class SingletonAssignment
{
    /// <summary>
    /// Returns the preferred host for every lock that at least one host can run.
    /// </summary>
    /// <param name="hosts">The live hosts. Order does not matter.</param>
    /// <param name="epsilon">
    /// How far above its fair share a host may go before the next ranked host is used. 0 spreads
    /// as evenly as the weights allow, larger values favour stability over balance.
    /// </param>
    public static IReadOnlyDictionary<string, string> Compute(
        IReadOnlyCollection<SingletonHostInfo> hosts,
        double epsilon = 0.25
    )
    {
        if (epsilon < 0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Must not be negative");

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (hosts.Count == 0)
            return result;

        //A lock advertised with different weights by different hosts (mixed versions during a
        //deploy) takes the largest, which is the same on every host
        var weights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var host in hosts)
        foreach (var (lockId, weight) in host.Locks)
            weights[lockId] = Math.Max(weights.GetValueOrDefault(lockId), Math.Max(1, weight));

        var total = weights.Values.Sum(w => (long)w);
        //A host is saturated once it holds an average share. Saturated hosts, typically the ones
        //holding a heavy lock, are left out when the cap for the remaining locks is worked out
        var saturation = (long)Math.Ceiling((double)total / hosts.Count);
        var load = hosts.ToDictionary(h => h.HostId, _ => 0L, StringComparer.Ordinal);
        var remaining = total;

        var ordered = weights
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal);
        foreach (var (lockId, weight) in ordered)
        {
            var open = hosts.Where(h => load[h.HostId] < saturation).ToList();
            //The cap on a host that still has room: its fair share of what is left, plus slack
            var cap =
                open.Count == 0
                    ? 0
                    : (long)
                        Math.Ceiling(
                            (1 + epsilon) * (remaining + open.Sum(h => load[h.HostId])) / open.Count
                        );

            var ranking = hosts
                .Where(h => h.Locks.ContainsKey(lockId))
                .OrderByDescending(h => Score(lockId, h.HostId))
                .ThenBy(h => h.HostId, StringComparer.Ordinal)
                .ToList();

            //The first ranked host with room, or an empty host: a lock heavier than the cap
            //still needs somewhere to go. If every candidate is full, use the least loaded
            var owner =
                ranking.FirstOrDefault(h =>
                    load[h.HostId] == 0
                    || (load[h.HostId] < saturation && load[h.HostId] + weight <= cap)
                ) ?? ranking.OrderBy(h => load[h.HostId]).First();

            load[owner.HostId] += weight;
            remaining -= weight;
            result[lockId] = owner.HostId;
        }

        return result;
    }

    /// <summary>
    /// A stable 64 bit score for a lock on a host. It must not depend on the runtime or process,
    /// so <see cref="string.GetHashCode()"/> cannot be used.
    /// </summary>
    internal static ulong Score(string lockId, string hostId)
    {
        //FNV-1a over lockId, a separator and hostId, then a finalizer to spread the bits
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(lockId))
            hash = (hash ^ b) * prime;
        hash = (hash ^ 0) * prime;
        foreach (var b in Encoding.UTF8.GetBytes(hostId))
            hash = (hash ^ b) * prime;

        hash ^= hash >> 33;
        hash *= 0xff51afd7ed558ccd;
        hash ^= hash >> 33;
        hash *= 0xc4ceb9fe1a85ec53;
        hash ^= hash >> 33;
        return hash;
    }
}
