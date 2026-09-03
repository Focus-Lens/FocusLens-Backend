using FluentValidation;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Common.Behaviours;

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        ValidationContext<TRequest> context = new(request);

        List<Error> errors = [.. (await Task.WhenAll(
                _validators.Select(validator => validator.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage))
            .Distinct()];

        if (errors.Count == 0)
        {
            return await next();
        }

        Type responseType = typeof(TResponse);

        if (responseType.IsGenericType
            && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            return (TResponse)Activator.CreateInstance(responseType, [null, errors, false])!;
        }

        throw new ValidationException(
            errors.Select(error => new FluentValidation.Results.ValidationFailure(
                error.Code,
                error.Description)));
    }
}
