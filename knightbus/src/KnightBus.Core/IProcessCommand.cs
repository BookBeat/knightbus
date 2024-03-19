using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Marks a class as a receiver of commands sent on a transport
/// </summary>
public interface IProcessCommand<T, TSettings> : IProcessMessage<T, Task>
    where T : ICommand
    where TSettings : class, IProcessingSettings, new()
{ }
