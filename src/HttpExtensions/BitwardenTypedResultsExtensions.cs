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
        /// <param name="extensions">
        /// Further problem members. A member named <c>errors</c> is dropped: that name belongs to
        /// <paramref name="errors"/>, and a document carrying it twice is not parseable.
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

            var problemDetails = new BitwardenValidationProblemDetails
            {
                Detail = detail,
                Instance = instance,
                Status = statusCode,
                Title = title,
                Type = type,
                Errors = new Dictionary<string, ErrorCode[]>(errors),
            };

            if (extensions is not null)
            {
                foreach (var (key, value) in extensions
                             .Where(extension => extension.Key != BitwardenValidationProblemDetails.ErrorsMember))
                {
                    problemDetails.Extensions[key] = value;
                }
            }

            return new BitwardenValidationProblemResult(problemDetails);
        }

        /// <summary>
        /// Produces an RFC 7807 problem response from a flat sequence of property/code pairs, grouping them by
        /// property so a caller that discovers failures one at a time does not have to build the dictionary itself.
        /// <remarks>
        /// WARNING: This is currently experimental and may change in the future.
        /// </remarks>
        /// </summary>
        /// <param name="errors">
        /// The failures, in the order they should appear under each property. More than one pair may name the same
        /// property; they are collected into that property's array rather than overwriting one another.
        /// </param>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IEnumerable<(string PropertyName, ErrorCode Code)> errors,
            string? detail = null,
            string? instance = null,
            string title = "One or more validation errors occurred.",
            string type = "validation_error",
            IDictionary<string, object?>? extensions = null,
            int statusCode = StatusCodes.Status400BadRequest)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var grouped = errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Code).ToArray());

            return TypedResults.BitwardenValidationProblem(
                errors: grouped,
                detail: detail,
                instance: instance,
                title: title,
                type: type,
                extensions: extensions,
                statusCode: statusCode);
        }
    }
}
