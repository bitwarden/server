using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.HttpExtensions;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// Renders <see cref="IValidationError"/>s as the Bitwarden problem response. Lives in Core rather than beside
/// <c>BitwardenValidationProblem</c> in HttpExtensions because it is the one layer that can see both sides:
/// HttpExtensions does not reference Core, so it cannot name <see cref="IValidationError"/>.
/// </summary>
public static class ValidationErrorTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces an RFC 7807 problem response keyed by <see cref="IValidationError.PropertyName"/>, with the
        /// error's <see cref="IValidationError.Type"/> as the i18n code and
        /// <see cref="IValidationError.Message"/> as the human-readable detail.
        /// </summary>
        /// <param name="statusCode">
        /// The response status. Defaults to 400 Bad Request. Pass another 4xx when the failure is carried by this
        /// same body but is not bad input — 409 Conflict for a state conflict, say.
        /// </param>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IValidationError validationError,
            string title = "One or more validation errors occurred.",
            string type = "validation_error",
            int statusCode = StatusCodes.Status400BadRequest)
        {
            ArgumentNullException.ThrowIfNull(validationError);

            return TypedResults.BitwardenValidationProblem(
                validationErrors: [validationError],
                title: title,
                type: type,
                statusCode: statusCode);
        }

        /// <inheritdoc cref="BitwardenValidationProblem(IValidationError, string, string, int)"/>
        /// <remarks>
        /// Errors naming the same property are collected under it rather than overwriting one another, so a model
        /// that fails several ways at once reports every failure.
        /// </remarks>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(
            IEnumerable<IValidationError> validationErrors,
            string title = "One or more validation errors occurred.",
            string type = "validation_error",
            int statusCode = StatusCodes.Status400BadRequest)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);

            return TypedResults.BitwardenValidationProblem(
                errors: validationErrors.Select(error => (
                    error.PropertyName,
                    new ErrorCode(error.Type, error.Message, error.Parameters))),
                title: title,
                type: type,
                statusCode: statusCode);
        }
    }
}
