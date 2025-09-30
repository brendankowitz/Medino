namespace Medino;

/// <summary>
/// Defines a handler for exceptions from a request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
/// <typeparam name="TException">Exception type</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when an exception of type <typeparamref name="TException"/> is thrown from request handling
    /// </summary>
    /// <param name="request">Request instance</param>
    /// <param name="exception">Exception instance</param>
    /// <param name="state">Current state of response handling. Check <see cref="RequestExceptionHandlerState{TResponse}.Handled"/> to see if exception handling is complete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task</returns>
    Task HandleAsync(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}

/// <summary>
/// State parameter for exception handlers to set response
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public class RequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// Gets whether the exception has been handled. When true, the response set in this state object will be returned and no further exception handlers will be called.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// Response to return when handled
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Mark the exception as handled and set the response
    /// </summary>
    /// <param name="response">Response to return</param>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}