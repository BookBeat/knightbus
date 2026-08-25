using System;

namespace KnightBus.Schedule;

public interface ISchedule
{
    /// <summary>
    /// Cron expression for schedule. <see cref="http://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html"/>
    /// </summary>
    string CronExpression { get; }

    /// <summary>
    /// TimeZone for the cron expression
    /// </summary>
    TimeZoneInfo TimeZone { get; }
}
