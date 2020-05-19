using System.Reflection;
using KnightBus.Core;

namespace KnightBus.Schedule
{
    public static class ScheduleExtensions
    {
        public static IHostConfiguration UseScheduling(this IHostConfiguration configuration)
        {
            var scheduler = new SchedulingPlugin(configuration);
            configuration.AddPlugin(scheduler);
            return configuration;
        }
        
        public static IHostConfiguration RegisterSchedules(this IHostConfiguration configuration, Assembly assembly)
        {
            configuration.DependencyInjection.RegisterOpenGeneric(typeof(IProcessSchedule<>), assembly);
            return configuration;
        }

        public static IDependencyInjection RegisterSchedules(this IDependencyInjection dependencyInjection, Assembly assembly)
        {
            dependencyInjection.RegisterOpenGeneric(typeof(IProcessSchedule<>), assembly);
            return dependencyInjection;
        }
    }
}