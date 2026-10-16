using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace KnightBus.Tests.Shared;

public static class Eventually
{
    /// <summary>
    /// Waits for <paramref name="condition"/>, and fails the test with <paramref name="describe"/>
    /// when it does not hold within <paramref name="timeout"/>, 10 seconds by default
    /// </summary>
    public static async Task True(
        Func<bool> condition,
        TimeSpan? timeout = null,
        Func<string>? describe = null
    )
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail(
                    "Timed out waiting for the condition"
                        + (describe == null ? "" : ": " + describe())
                );
            await Task.Delay(10);
        }
    }
}
