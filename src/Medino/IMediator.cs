namespace Medino;

/// <summary>
/// Defines a mediator to encapsulate request/response and publishing interaction patterns
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Asynchronously send a command to a single handler
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <param name="command">Command object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>
    /// Asynchronously send a request to a single handler and get a response
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="request">Request object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that represents the response</returns>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously publish a notification to multiple handlers
    /// </summary>
    /// <typeparam name="TNotification">Notification type</typeparam>
    /// <param name="notification">Notification object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}