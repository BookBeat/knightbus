using System.Reflection;
using KnightBus.Core;
using KnightBus.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
        
        public static IServiceCollection RegisterSchedules(this IServiceCollection collection, Assembly assembly)
        {
            collection.RegisterGenericProcessor(typeof(IProcessSchedule<>), assembly);
            return collection;
        }
    }
}