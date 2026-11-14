using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.HttpExtensions;

namespace Microsoft.AspNetCore.Http.HttpResults;

public static class ValidationErrorTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces an RFC 7807 problem response (400 Bad Request unless the error sets
        /// <see cref="IValidationError.StatusCode"/>) keyed by
        /// <see cref="IValidationError.PropertyName"/>, with the error's
        /// <see cref="IValidationError.Type"/> as the i18n code and
        /// <see cref="IValidationError.Message"/> as the human-readable detail.
        /// </summary>
        public static BitwardenValidationProblemResult BitwardenValidationProblem(IValidationError validationError)
        {
            ArgumentNullException.ThrowIfNull(validationError);

            return TypedResults.BitwardenValidationProblem(
                errors: new Dictionary<string, BitwardenTypedResultsExtensions.ErrorCode[]>
                {
                    {
                        validationError.PropertyName,
                        [new BitwardenTypedResultsExtensions.ErrorCode(validationError.Type, validationError.Message)]
                    }
                },
                statusCode: validationError.StatusCode);
        }
    }
}
