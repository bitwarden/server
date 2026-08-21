using Bit.HttpExtensions;

namespace Microsoft.AspNetCore.Http.HttpResults;

public static class BitwardenTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces an RFC 7807 problem response, mirroring <c>TypedResults.ValidationProblem</c> but typing
        /// each error entry as an array of <see cref="ErrorCode"/> rather than <c>string[]</c>.
        /// <remarks>
        /// WARNING: This is currently experimental and may change in the future.
        /// </remarks>
        /// </summary>
        /// <param name="statusCode">
        /// The response status. Defaults to 400 Bad Request. Pass another 4xx when the failure is carried by this
        /// same body but is not bad input — 409 Conflict for a state conflict, say — so a caller that only reads
        /// the status still learns something before it reads the codes.
        /// </param>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IDictionary<string, ErrorCode[]> errors,
            string? detail = null,
            string? instance = null,
            string title = "One or more validation errors occurred.",
            string type = "validation_error",
            IDictionary<string, object?>? extensions = null,
            int statusCode = StatusCodes.Status400BadRequest)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var problemExtensions = extensions is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(extensions);

            problemExtensions["errors"] = errors;

            return new BitwardenValidationProblemResult(TypedResults.Problem(
                detail: detail,
                instance: instance,
                statusCode: statusCode,
                title: title,
                type: type,
                extensions: problemExtensions));
        }
    }

    public record ErrorCode(string Type, string Detail);
}
