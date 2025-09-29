namespace Medino.Tests.PipelineBehaviors;

public class ValidatableRequestValidationBehavior : IPipelineBehavior<ValidatableRequest, ValidatableResponse>
{
    public async Task<ValidatableResponse> HandleAsync(ValidatableRequest request, RequestHandlerDelegate<ValidatableResponse> next, CancellationToken cancellationToken)
    {
        if (!request.IsValid)
        {
            throw new ValidationException($"Validation failed for {nameof(ValidatableRequest)}");
        }

        return await next();
    }
}

public record ValidatableRequest : IRequest<ValidatableResponse>, IValidatable
{
    public bool IsValid { get; set; }
}

public class ValidatableResponse
{
    public bool Success { get; set; } = true;
}

public class ValidatableRequestHandler : IRequestHandler<ValidatableRequest, ValidatableResponse>
{
    public Task<ValidatableResponse> HandleAsync(ValidatableRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ValidatableResponse());
    }
}