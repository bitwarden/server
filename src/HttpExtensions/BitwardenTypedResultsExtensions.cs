using Bit.HttpExtensions;

namespace Microsoft.AspNetCore.Http.HttpResults;

public static class BitwardenTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces an RFC 7807 problem response, mirroring <c>TypedResults.ValidationProblem</c> but typing
        /// each error entry as an array of <see cref="ErrorCode"/> rather than <c>string[]</c>.
        /// Defaults to 400 Bad Request. When <paramref name="type"/> or <paramref name="title"/> is omitted, the
        /// missing value is taken from <paramref name="statusCode"/>; only 400 and 404 have defaults.
        /// <remarks>
        /// WARNING: This is currently experimental and may change in the future.
        /// </remarks>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="type"/> or <paramref name="title"/> is omitted and <paramref name="statusCode"/> has no default.
        /// </exception>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IDictionary<string, ErrorCode[]> errors,
            string? detail = null,
            string? instance = null,
            string? title = null,
            string? type = null,
            IDictionary<string, object?>? extensions = null,
            int statusCode = StatusCodes.Status400BadRequest)
        {
            ArgumentNullException.ThrowIfNull(errors);

            if (type is null || title is null)
            {
                var (defaultType, defaultTitle) = statusCode switch
                {
                    StatusCodes.Status400BadRequest => ("validation_error", "One or more validation errors occurred."),
                    StatusCodes.Status404NotFound => ("not_found", "The requested resource was not found."),
                    _ => throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode,
                        "No default type and title exist for this status code. Provide both explicitly.")
                };
                type ??= defaultType;
                title ??= defaultTitle;
            }

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
