using System;

namespace KnightBus.Health;

public static class RemediationExtensions
{
    public static string GetRemediationText<T>(this Type command)
    {
        var remediationMapping = command.GetInterface(nameof(IRemediationMapping));
        if (remediationMapping == null)
            return "";

        var remediationMessageProperty = remediationMapping.GetProperty(
            nameof(IRemediationMapping.RemediationMessage)
        );
        if (remediationMessageProperty == null)
            return "";

        var remediationText = remediationMessageProperty.GetValue(command) as string;
        if (remediationText == null)
            return "";

        return remediationText;
    }
}
