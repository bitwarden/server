using System.Diagnostics;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.Utilities.Validation;

public abstract record ValidationResult<T>(T Value);

public record Valid<T>(T Value) : ValidationResult<T>(Value);

public record Invalid<T>(T Value, Error Error) : ValidationResult<T>(Value);

public static class ValidationResultMappers
{
    public static ValidationResult<B> Map<A, B>(this ValidationResult<A> validationResult, B invalidValue) =>
        validationResult switch
        {
            Valid<A> => new Valid<B>(invalidValue),
            Invalid<A> invalid => new Invalid<B>(invalid.Error.ToError(invalidValue)),
            _ => throw new ArgumentOutOfRangeException(nameof(validationResult), "Unhandled validation result type")
        };

    public static CommandResult<B> ToCommandResult<A, B>(this ValidationResult<A> validationResult, B commandValue) =>
        validationResult switch
        {
            Valid<A> => new Success<B>(commandValue),
            Invalid<A> invalid => new Failure<B>(commandValue, invalid.Error),
            _ => throw new UnreachableException()
        };
}
