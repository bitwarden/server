using Bit.Core.Models.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Utilities;

public static class CommandResultExtensions
{

    public static IActionResult MapToAction<T>(this CommandResult<T> commandResult)
    {
        return commandResult switch
        {
            NoRecordFoundFailure<T> failure => new ObjectResult(failure.ErrorMessage) { StatusCode = StatusCodes.Status404NotFound },
            BadRequestFailure<T> failure => new ObjectResult(failure.ErrorMessage) { StatusCode = StatusCodes.Status400BadRequest },
            Failure<T> failure => new ObjectResult(failure.ErrorMessage) { StatusCode = StatusCodes.Status400BadRequest },
            Success<T> success => new ObjectResult(success.Value) { StatusCode = StatusCodes.Status200OK },
            _ => throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}")
        };
    }
}
