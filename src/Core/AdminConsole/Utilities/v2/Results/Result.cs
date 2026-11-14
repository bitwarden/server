using OneOf;

namespace Bit.Core.AdminConsole.Utilities.v2.Results;

/// <summary>
/// Represents the result of an operation whose failure is a specific, typed error.
/// This is a <see cref="OneOf{TError, T}"/> type that contains a <typeparamref name="TError"/> if the operation failed, or its result if it succeeded.
/// Use this instead of <see cref="CommandResult{T}"/> when callers need the concrete error type.
/// </summary>
/// <typeparam name="T">The type of the successful result.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public class Result<T, TError>(OneOf<TError, T> result) : OneOfBase<TError, T>(result) where TError : Error
{
    public bool IsError => IsT0;
    public bool IsSuccess => IsT1;
    public TError AsError => AsT0;
    public T AsSuccess => AsT1;

    public static implicit operator Result<T, TError>(T value) => new(value);
    public static implicit operator Result<T, TError>(TError error) => new(error);
}
