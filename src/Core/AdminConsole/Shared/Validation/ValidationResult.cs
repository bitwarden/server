using Bit.Core.AdminConsole.Errors;

namespace Bit.Core.AdminConsole.Shared.Validation;

public abstract record ValidationResult<T>;

public record Valid<T>(T Value) : ValidationResult<T>;

public record Invalid<T>(Error<T> Error) : ValidationResult<T>;

public record PartialValidationResult<T> : ValidationResult<T>
{
    public IEnumerable<T> Valid { get; init; } = [];
    public IEnumerable<Error<T>> Invalid { get; init; } = [];
}

public static class ValidationResultMappers
{
    public static ValidationResult<B> Map<A, B>(this ValidationResult<A> validationResult, B invalidValue) =>
        validationResult switch
        {
            Valid<A> => new Valid<B>(invalidValue),
            Invalid<A> invalid => new Invalid<B>(invalid.Error.ToError(invalidValue)),
            _ => throw new ArgumentOutOfRangeException(nameof(validationResult), "Unhandled validation result type")
        };
}
