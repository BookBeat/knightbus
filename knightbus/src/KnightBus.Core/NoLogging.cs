using System;

namespace KnightBus.Core
{
    public class NoLogging : ILog
    {
        public void Error(Exception e, string template, params object[] objects)
        {
        }

        public void Error(string template, params object[] objects)
        {
        }

        public void Information(Exception e, string template, params object[] objects)
        {
        }

        public void Information(string template, params object[] objects)
        {
        }

        public void Debug(Exception e, string template, params object[] objects)
        {
        }

        public void Debug(string template, params object[] objects)
        {
        }

        public void Warning(Exception e, string template, params object[] objects)
        {
        }

        public void Warning(string template, params object[] objects)
        {
        }
    }
}