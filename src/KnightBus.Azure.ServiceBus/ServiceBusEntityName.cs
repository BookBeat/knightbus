using System;

namespace KnightBus.Azure.ServiceBus;

internal static class ServiceBusEntityName
{
    /// <summary>
    /// Service Bus reports a forwarding target either as a bare entity name or as an absolute URI
    /// of the entity, so this returns just the name, or null when nothing is forwarded.
    /// </summary>
    public static string? FromForwardTo(string? forwardTo)
    {
        if (string.IsNullOrWhiteSpace(forwardTo))
            return null;
        if (
            Uri.TryCreate(forwardTo, UriKind.Absolute, out var uri)
            && !string.IsNullOrEmpty(uri.Host)
        )
            return Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        return forwardTo.Trim('/');
    }

    /// <summary>
    /// Entity names are case-insensitive in Service Bus.
    /// </summary>
    public static bool AreSame(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
