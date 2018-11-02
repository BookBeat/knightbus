using System;

namespace KnightBus.Host.Singleton
{
    public class SingletonLockManagerMissingException : Exception
    {
        public SingletonLockManagerMissingException(): base("There is no ISingletonLockManager specified, you cannot use the ISingletonProcessor directive without one")
        {}
    }
}