using System.Reflection;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Schedule;

public static class ScheduleExtensions
{
    public static IServiceCollection UseScheduling(this IServiceCollection collection)
    {
        collection.AddPlugin<SchedulingPlugin>();
        return collection;
    }

    public static IServiceCollection RegisterSchedules(
        this IServiceCollection collection,
        Assembly assembly
    )
    {
        collection.RegisterGenericProcessor(typeof(IProcessSchedule<>), assembly);
        return collection;
    }

    internal static IServiceCollection RegisterSchedule<TProcessor, TSchedule>(
        this IServiceCollection collection
    )
        where TProcessor : class, IProcessSchedule<TSchedule>
        where TSchedule : class, ISchedule, new()
    {
        collection.RegisterGenericProcessor(typeof(TProcessor), typeof(IProcessSchedule<>));
        return collection;
    }

    public static IServiceCollection RegisterSchedules(this IServiceCollection collection)
    {
        collection.RegisterGenericProcessor(
            typeof(IProcessSchedule<>),
            Assembly.GetCallingAssembly()
        );
        return collection;
    }
}
