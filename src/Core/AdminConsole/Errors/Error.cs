using Bit.Core.Models.Commands;

namespace Bit.Core.AdminConsole.Errors;

public record Error<T>(string Message, T ErroredValue);

public static class ErrorMappers
{
    public static Error<B> ToError<A, B>(this Error<A> errorA, B erroredValue) => new(errorA.Message, erroredValue);

    public static Failure<CommandType> ToFailure<ValidationType, CommandType>(this Error<ValidationType> error)
    {
        return error switch
        {
            BadRequestError<ValidationType> badRequest => new BadRequestFailure<CommandType>(badRequest.Message),
            RecordNotFoundError<ValidationType> recordNotFound => new NoRecordFoundFailure<CommandType>(recordNotFound.Message),
            _ => throw new InvalidOperationException($"Unhandled Error type: {error.GetType().Name}")
        };
    }
}
