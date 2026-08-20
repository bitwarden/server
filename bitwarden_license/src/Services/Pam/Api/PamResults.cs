using Bit.Core.AdminConsole.Utilities.v2.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bit.Services.Pam.Api;

/// <summary>
/// Turns a command's <see cref="CommandResult{T}"/> into the HTTP response for it, so every PAM handler renders
/// success and failure the same way. The minimal-API counterpart of <c>BaseAdminConsoleController.Handle</c>.
/// </summary>
/// <remarks>
/// The declared <c>Results&lt;…&gt;</c> union is what puts the success schema in the generated OpenAPI — and so in
/// the SDK's bindings — while keeping every failure on the single <see cref="PamErrorResult"/> arm.
/// </remarks>
public static class PamResults
{
    /// <summary>
    /// Maps a command result to <c>200 OK</c> carrying <paramref name="response"/> applied to the value, or to the
    /// error's problem response.
    /// </summary>
    public static Results<Ok<TResponse>, PamErrorResult> Ok<T, TResponse>(
        CommandResult<T> result, Func<T, TResponse> response) =>
        result.Match<Results<Ok<TResponse>, PamErrorResult>>(
            error => PamErrorResult.From(error),
            value => TypedResults.Ok(response(value)));

    /// <summary>
    /// Maps a command result with no value to <c>204 No Content</c>, or to the error's problem response.
    /// </summary>
    public static Results<NoContent, PamErrorResult> NoContent(CommandResult result) =>
        result.Match<Results<NoContent, PamErrorResult>>(
            error => PamErrorResult.From(error),
            _ => TypedResults.NoContent());
}
