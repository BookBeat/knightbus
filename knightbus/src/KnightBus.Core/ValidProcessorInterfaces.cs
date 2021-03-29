using System;

namespace KnightBus.Core
{
    /// <summary>
    /// List of valid interfaces for processors that can used to process messages
    /// </summary>
    public static class ValidProcessorInterfaces
    {
        public static Type[] Types = { typeof(IProcessCommand<,>), typeof(IProcessEvent<,,>), typeof(IProcessRequest<,,>) };
    }
}