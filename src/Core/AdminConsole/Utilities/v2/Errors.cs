namespace Bit.Core.AdminConsole.Utilities.v2;

/// <summary>
/// A strongly typed error containing a reason that an action failed.
/// This is used for business logic validation and other expected errors, not exceptions.
/// </summary>
public abstract record Error(string Message);
/// <summary>
/// An <see cref="Error"/> type that maps to a NotFoundResult at the api layer.
/// </summary>
/// <param name="Message"></param>
public abstract record NotFoundError(string Message) : Error(Message);

public abstract record BadRequestError(string Message) : Error(Message);
public abstract record InternalError(string Message) : Error(Message);

public record AggregateError(string Message) : Error(Message);
public record AggregateInternalError(string Message) : InternalError(Message);
public record AggregateNotFoundError(string Message) : NotFoundError(Message);
public record AggregateBadRequestError(string Message) : BadRequestError(Message);

public static class ErrorFunctions
{
    public static Error Fold(this ICollection<Error> errors, string separator = "; ")
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("Cannot fold an empty collection of errors.", nameof(errors));
        }

        var messages = string.Join(separator, errors.Select(e => e.Message));

        return (errors.All(e => e is InternalError),
                errors.All(e => e is NotFoundError),
                errors.All(e => e is BadRequestError)) switch
        {
            (true, _, _) => new AggregateInternalError(messages),
            (_, true, _) => new AggregateNotFoundError(messages),
            (_, _, true) => new AggregateBadRequestError(messages),
            _ => new AggregateError(messages)
        };
    }
}
