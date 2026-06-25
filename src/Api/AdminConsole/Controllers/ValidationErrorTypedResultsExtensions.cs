using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Microsoft.AspNetCore.Http.HttpResults;

public static class ValidationErrorTypedResultsExtensions
{
    extension(TypedResults)
    {
        /// <summary>
        /// Produces a 400 Bad Request RFC 7807 problem response keyed by
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
                });
        }
    }
}
