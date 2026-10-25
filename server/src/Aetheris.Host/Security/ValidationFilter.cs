using FluentValidation;
using FluentValidation.Results;

namespace Aetheris.Host.Security;

/// <summary>
/// Validates every argument of an endpoint that has a FluentValidation validator.
/// </summary>
/// <remarks>
/// The filter is added once, to the whole API group, so a new endpoint is validated as
/// soon as its request type has a validator class (task 00002, section 2.3). A request that
/// fails gets <c>400</c> with the <c>validation_failed</c> code and the failed rules.
/// </remarks>
public static class ValidationFilter
{
    /// <summary>
    /// Adds the validation filter to every endpoint of a group.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder.</typeparam>
    /// <param name="builder">The group or endpoint.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static TBuilder WithValidation<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            // Only the body-like arguments are validated; route values and services are not.
            List<(int Index, Type ValidatorType)> validated = factoryContext.MethodInfo
                .GetParameters()
                .Select((parameter, index) => (Index: index, Type: parameter.ParameterType))
                .Where(parameter => parameter.Type.IsClass && parameter.Type != typeof(string))
                .Select(parameter => (parameter.Index, ValidatorType: typeof(IValidator<>).MakeGenericType(parameter.Type)))
                .ToList();

            if (validated.Count == 0)
            {
                return next;
            }

            return async invocationContext =>
            {
                IServiceProvider services = invocationContext.HttpContext.RequestServices;

                foreach ((int index, Type validatorType) in validated)
                {
                    object? argument = invocationContext.Arguments[index];

                    if (argument is null || services.GetService(validatorType) is not IValidator validator)
                    {
                        continue;
                    }

                    ValidationResult result = await validator
                        .ValidateAsync(new ValidationContext<object>(argument), invocationContext.HttpContext.RequestAborted)
                        .ConfigureAwait(false);

                    if (!result.IsValid)
                    {
                        return ApiErrors.Validation(ToDetails(result));
                    }
                }

                return await next(invocationContext).ConfigureAwait(false);
            };
        });

        return builder;
    }

    /// <summary>
    /// Groups the failed rules by property.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <returns>The failed rule codes, keyed by property name.</returns>
    /// <remarks>
    /// The error codes of the rules are sent, not the messages: a message may repeat the
    /// value that was sent, and the value can be a password.
    /// </remarks>
    public static IReadOnlyDictionary<string, string[]> ToDetails(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }
}
