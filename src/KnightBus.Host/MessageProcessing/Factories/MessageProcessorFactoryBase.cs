using System;
using System.Collections.Generic;
using KnightBus.Core;

namespace KnightBus.Host.MessageProcessing.Factories;

internal abstract class MessageProcessorFactoryBase
{
    private readonly Type _genericInterface;

    protected MessageProcessorFactoryBase(Type genericInterface)
    {
        _genericInterface = genericInterface;
    }

    public IEnumerable<Type> GetInterfaces(Type processorType)
    {
        return ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(
            processorType,
            _genericInterface
        );
    }

    public bool CanCreate(Type processorInterface)
    {
        return processorInterface.IsGenericType
            && processorInterface.GetGenericTypeDefinition() == _genericInterface;
    }
}
