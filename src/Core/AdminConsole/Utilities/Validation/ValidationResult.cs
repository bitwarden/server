using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.Utilities.Validation;

public abstract record ValidationResult<T>;

public record Valid<T>(T Value) : ValidationResult<T>;

public record Invalid<T>(Error<T> Error) : ValidationResult<T>;

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
