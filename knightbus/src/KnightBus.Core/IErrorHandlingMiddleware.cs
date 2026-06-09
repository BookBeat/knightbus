namespace KnightBus.Core;

/// <summary>
/// Middleware responsible for handling exceptions thrown during message processing. It is always
/// placed as the outermost middleware.
///
/// The default implementation is <see cref="DefaultMiddlewares.ErrorHandlingMiddleware"/>.
/// </summary>
public interface IErrorHandlingMiddleware : IMessageProcessorMiddleware { }
