using KnightBus.Core;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.ApplicationInsights;

public static class ApplicationInsightsExtensions
{
    public static IServiceCollection UseApplicationInsights(this IServiceCollection services, TelemetryConfiguration telemetryConfiguration)
    {
        InitializeDependencyTracking(telemetryConfiguration);
        services.AddMiddleware(new ApplicationInsightsMessageMiddleware(telemetryConfiguration));
        return services;
    }

    public static IHostConfiguration EnableLiveMetricsStream(this IHostConfiguration hostConfiguration, TelemetryConfiguration telemetryConfiguration)
    {
        QuickPulseTelemetryProcessor processor = null;

        telemetryConfiguration.TelemetryProcessorChainBuilder
            .Use(next =>
            {
                processor = new QuickPulseTelemetryProcessor(next);
                return processor;
            })
            .Build();

        var quickPulse = new QuickPulseTelemetryModule();
        quickPulse.Initialize(telemetryConfiguration);
        quickPulse.RegisterTelemetryProcessor(processor);
        foreach (var telemetryProcessor in telemetryConfiguration.TelemetryProcessors)
        {
            if (telemetryProcessor is ITelemetryModule telemetryModule)
            {
                telemetryModule.Initialize(telemetryConfiguration);
            }
        }
        return hostConfiguration;
    }

    private static DependencyTrackingTelemetryModule InitializeDependencyTracking(TelemetryConfiguration configuration)
    {
        var module = new DependencyTrackingTelemetryModule();

        // prevent Correlation Id to be sent to certain endpoints. You may add other domains as needed.
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("localhost");
        module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");

        // enable known dependency tracking, note that in future versions, we will extend this list. 
        // please check default settings in https://github.com/Microsoft/ApplicationInsights-dotnet-server/blob/develop/Src/DependencyCollector/DependencyCollector/ApplicationInsights.config.install.xdt

        module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
        module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");

        // initialize the module
        module.Initialize(configuration);

        return module;
    }
}
