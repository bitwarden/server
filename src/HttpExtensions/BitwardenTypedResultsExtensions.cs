using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Microsoft.AspNetCore.Http.HttpResults;

public static class BitwardenTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces a 400 Bad Request RFC 7807 problem response, mirroring
        /// <c>TypedResults.ValidationProblem</c> but typing each error entry as an array of
        /// <see cref="ErrorCode"/> rather than <c>string[]</c>.
        /// <remarks>
        /// WARNING: This is currently experimental and may change in the future.
        /// </remarks>
        /// </summary>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IDictionary<string, ErrorCode[]> errors,
            string? detail = null,
            string? instance = null,
            string title = "One or more validation errors occurred.",
            string type = "validation_error",
            IDictionary<string, object?>? extensions = null)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var problemExtensions = extensions is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(extensions);

            problemExtensions["errors"] = errors;

            return new BitwardenValidationProblemResult(TypedResults.Problem(
                detail: detail,
                instance: instance,
                statusCode: StatusCodes.Status400BadRequest,
                title: title,
                type: type,
                extensions: problemExtensions));
        }

        public static BitwardenValidationProblemResult BitwardenValidationProblem(IValidationError validationError)
        {
            ArgumentNullException.ThrowIfNull(validationError);

            return TypedResults.BitwardenValidationProblem(
                errors: new Dictionary<string, ErrorCode[]>
                {
                    { validationError.PropertyName, [new ErrorCode(validationError.Type, validationError.Message)] }
                });
        }
    }

    public record ErrorCode(string Type, string Detail);
}
