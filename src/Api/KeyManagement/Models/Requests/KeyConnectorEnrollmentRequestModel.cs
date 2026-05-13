using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class KeyConnectorEnrollmentRequestModel : IValidatableObject
{
    [EncryptedString]
    public required string KeyConnectorKeyWrappedUserKey { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(KeyConnectorKeyWrappedUserKey))
        {
            yield return new ValidationResult(
                "KeyConnectorKeyWrappedUserKey must be supplied when request body is provided.",
                [nameof(KeyConnectorKeyWrappedUserKey)]);
        }
    }
}
