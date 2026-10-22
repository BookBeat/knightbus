using System;
using System.Security.Cryptography;

namespace KnightBus.Azure.Storage.Singleton;

/// <summary>
/// Names this process among the hosts that share the singleton locks: the machine name (the pod name
/// in Kubernetes) and a suffix that changes with every process start, so a restarted process is a
/// new host. The same id is used in the logs, the lock blob metadata and the placement members.
/// </summary>
internal static class HostIdentity
{
    internal static string Id { get; } =
        $"{Environment.MachineName}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant()}";
}
