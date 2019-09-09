namespace KnightBus.Schedule
{
    public interface ISchedule
    {
        string CronExpression { get; }
    }
}