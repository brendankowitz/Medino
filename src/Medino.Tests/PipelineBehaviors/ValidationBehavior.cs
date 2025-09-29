namespace Medino.Tests.PipelineBehaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IValidatable validatable && !validatable.IsValid)
        {
            throw new ValidationException($"Validation failed for {typeof(TRequest).Name}");
        }

        return await next();
    }
}

public interface IValidatable
{
    bool IsValid { get; }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}