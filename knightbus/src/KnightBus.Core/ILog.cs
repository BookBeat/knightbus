using System;

namespace KnightBus.Core
{
    public interface ILog
    {
        void Error(Exception e, string template, params object[] objects);
        void Error(string template, params object[] objects);
        void Information(Exception e, string template, params object[] objects);
        void Information(string template, params object[] objects);
        void Debug(Exception e, string template, params object[] objects);
        void Debug(string template, params object[] objects);
        void Warning(Exception e, string template, params object[] objects);
        void Warning(string template, params object[] objects);
    }
}