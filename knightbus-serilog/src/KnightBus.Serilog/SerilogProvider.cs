using System;
using KnightBus.Core;
using Serilog;

namespace KnightBus.Serilog
{
    public class SerilogProvider: ILog
    {
        private readonly ILogger _logger;

        public SerilogProvider(ILogger logger)
        {
            _logger = logger;
        }
        public void Error(Exception e, string template, params object[] objects)
        {
            _logger.Error(e, template, objects);
        }

        public void Error(string template, params object[] objects)
        {
            _logger.Error(template, objects);
        }

        public void Information(Exception e, string template, params object[] objects)
        {
            _logger.Information(e, template, objects);
        }

        public void Information(string template, params object[] objects)
        {
            _logger.Information(template, objects);
        }

        public void Debug(Exception e, string template, params object[] objects)
        {
            _logger.Debug(e, template, objects);
        }

        public void Debug(string template, params object[] objects)
        {
            _logger.Debug(template, objects);
        }

        public void Warning(Exception e, string template, params object[] objects)
        {
            _logger.Warning(e, template, objects);
        }

        public void Warning(string template, params object[] objects)
        {
            _logger.Warning(template, objects);
        }
    } 
}
