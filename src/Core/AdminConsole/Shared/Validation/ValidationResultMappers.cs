namespace Bit.Core.AdminConsole.Shared.Validation;

public static class ValidationResultMappers
{
    // public static Failure MapToFailure<T>(this Error<T> error)
    // {
    //
    //     return error switch
    //     {
    //         BadRequestError<T> badRequestError => new Failure(badRequestError.Message),
    //         _ => throw new InvalidOperationException($"Unhandled commandResult type: {error.GetType().Name}")
    //     }
    //     // return commandResult switch
    //     // {
    //     //     NoRecordFoundFailure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status404NotFound },
    //     //     BadRequestFailure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
    //     //     Failure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
    //     //     Success<T> success => new ObjectResult(success.Value) { StatusCode = StatusCodes.Status200OK },
    //     //     _ => throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}")
    //     // };
    // }
}
