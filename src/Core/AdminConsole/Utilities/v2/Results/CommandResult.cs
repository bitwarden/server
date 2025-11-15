using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.Utilities.v2.Results;

/// <summary>
/// Represents the result of a command.
/// This is a <see cref="OneOf{Error, T}"/> type that contains an Error if the command execution failed, or the result of the command if it succeeded.
/// </summary>
/// <typeparam name="T">The type of the successful result. If there is no successful result (void), use <see cref="BulkCommandResult"/>.</typeparam>

public class CommandResult<T>(OneOf<Error, T> result) : OneOfBase<Error, T>(result)
{
    public bool IsError => IsT0;
    public bool IsSuccess => IsT1;
    public Error AsError => AsT0;
    public T AsSuccess => AsT1;

    public static implicit operator CommandResult<T>(T value) => new(value);
    public static implicit operator CommandResult<T>(Error error) => new(error);
}

/// <summary>
/// Represents the result of a command where successful execution returns no value (void).
/// See <see cref="CommandResult{T}"/> for more information.
/// </summary>
public class CommandResult(OneOf<Error, None> result) : CommandResult<None>(result)
{
    public static implicit operator CommandResult(None none) => new(none);
    public static implicit operator CommandResult(Error error) => new(error);
}

/// <summary>
/// A wrapper for <see cref="CommandResult{T}"/> with an ID, to identify the result in bulk operations.
/// </summary>
public record BulkCommandResult<T>(Guid Id, CommandResult<T> Result);

/// <summary>
/// A wrapper for <see cref="CommandResult"/> with an ID, to identify the result in bulk operations.
/// </summary>
public record BulkCommandResult(Guid Id, CommandResult Result);
