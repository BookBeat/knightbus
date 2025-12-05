using System.Diagnostics;
using System.Reflection;

namespace KnightBus.OpenTelemetry;

public static class KnightBusDiagnostics
{
    public static readonly string Name = "KnightBus";

    public static readonly string Version =
        typeof(KnightBusDiagnostics)
            .Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version
        ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(Name, Version);
}
