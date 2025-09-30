namespace Medino.Tests.PipelineBehaviors;

public class ValidatableObjectValidationBehavior : IPipelineBehavior<object, ValidatableResponse>
{
    public async Task<ValidatableResponse> HandleAsync(object request, RequestHandlerDelegate<ValidatableResponse> next, CancellationToken cancellationToken)
    {
        if (request is IValidatable validatable && !validatable.IsValid)
        {
            throw new ValidationException($"Validation failed for {request.GetType().Name}");
        }

        return await next();
    }
}