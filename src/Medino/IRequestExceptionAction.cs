namespace Medino;

/// <summary>
/// Defines an action to take when an exception is thrown during request handling.
/// This is called after all exception handlers and allows for logging, telemetry, or exception translation.
/// If this action throws a different exception, that exception will be thrown instead of the original.
/// This allows for exception translation/wrapping scenarios.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TException">Exception type</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when an exception of type <typeparamref name="TException"/> is thrown.
    /// If this method throws a different exception, that exception will replace the original exception.
    /// </summary>
    /// <param name="request">Request instance</param>
    /// <param name="exception">Exception instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that completes when the action is done, or throws an exception to translate the original</returns>
    Task ExecuteAsync(TRequest request, TException exception, CancellationToken cancellationToken);
}