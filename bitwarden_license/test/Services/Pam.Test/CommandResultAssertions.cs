using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Services.Pam.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.Services.Pam.Test;

/// <summary>
/// Unwraps a <see cref="CommandResult{T}"/> in a test, failing with the other side's content when it is not the side
/// the test expected — so a command that starts returning an error reports <em>which</em> error rather than a
/// null-reference or a cast.
/// </summary>
public static class CommandResultAssertions
{
    /// <summary>Asserts the command succeeded and returns its value.</summary>
    public static T AssertSuccess<T>(this CommandResult<T> result)
    {
        Assert.True(result.IsSuccess, $"Expected success, got {Describe(result)}.");
        return result.AsSuccess;
    }

    /// <summary>
    /// Asserts the command failed and returns the error, for the caller to match against an expected type:
    /// <c>Assert.IsType&lt;AccessAlreadyActive&gt;(result.AssertError())</c>.
    /// </summary>
    public static Error AssertError<T>(this CommandResult<T> result)
    {
        Assert.True(result.IsError, "Expected an error, got success.");
        return result.AsError;
    }

    /// <summary>Asserts a handler returned <c>200 OK</c> and returns the response body.</summary>
    public static TValue AssertOk<TValue>(this Results<Ok<TValue>, PamErrorResult> result) =>
        Assert.IsType<Ok<TValue>>(result.Result).Value!;

    /// <summary>Asserts a handler returned <c>204 No Content</c>.</summary>
    public static void AssertNoContent(this Results<NoContent, PamErrorResult> result) =>
        Assert.IsType<NoContent>(result.Result);

    /// <summary>Asserts a handler failed, and returns the error it is about to render as a problem response.</summary>
    public static Error AssertError<TValue>(this Results<Ok<TValue>, PamErrorResult> result) =>
        Assert.IsType<PamErrorResult>(result.Result).Error;

    /// <inheritdoc cref="AssertError{TValue}(Results{Ok{TValue}, PamErrorResult})"/>
    public static Error AssertError(this Results<NoContent, PamErrorResult> result) =>
        Assert.IsType<PamErrorResult>(result.Result).Error;

    private static string Describe<T>(CommandResult<T> result) =>
        result.IsError ? $"{result.AsError.GetType().Name} (\"{result.AsError.Message}\")" : "success";
}
