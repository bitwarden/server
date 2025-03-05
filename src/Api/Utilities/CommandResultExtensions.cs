using Bit.Api.Models.CommandResults;
using Bit.Core.Models.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Utilities;

public static class CommandResultExtensions
{
    public static IActionResult MapToActionResult<T>(this CommandResult<T> commandResult)
    {
        return commandResult switch
        {
            NoRecordFoundFailure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status404NotFound },
            BadRequestFailure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
            Failure<T> failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
            Success<T> success => new ObjectResult(success.Data) { StatusCode = StatusCodes.Status200OK },
            _ => throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}")
        };
    }

    public static IActionResult MapToActionResult(this CommandResult commandResult)
    {
        return commandResult switch
        {
            NoRecordFoundFailure failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status404NotFound },
            BadRequestFailure failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
            Failure failure => new ObjectResult(failure.ErrorMessages) { StatusCode = StatusCodes.Status400BadRequest },
            Success => new ObjectResult(new { }) { StatusCode = StatusCodes.Status200OK },
            _ => throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}")
        };
    }
}
