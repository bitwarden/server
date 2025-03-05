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
            NoRecordFoundFailure<T> => new ObjectResult(commandResult.Errors) { StatusCode = StatusCodes.Status404NotFound },
            BadRequestFailure<T> or FailureCommandResult<T> => new ObjectResult(commandResult.Errors) { StatusCode = StatusCodes.Status400BadRequest },
            SuccessCommandResult<T> => new ObjectResult(commandResult.Data) { StatusCode = StatusCodes.Status200OK },
            _ => throw new InvalidOperationException($"Unhandled commandResult type: {commandResult.GetType().Name}")
        };
    }
}
