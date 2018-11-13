using System;

namespace KnightBus.Core
{
    public class MessageMappingMissingException : Exception
    {
        public MessageMappingMissingException(string message) : base(message)
        {
            
        }
    }
}