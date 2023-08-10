using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessTokenCreateRequestModel : IValidatableObject
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(200)]
    public string Name { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(4000)]
    public string EncryptedPayload { get; set; }

    [Required]
    [EncryptedString]
    public string Key { get; set; }

    public DateTime? ExpireAt { get; set; }

    public ApiKey ToApiKey(Guid serviceAccountId)
    {
        return new ApiKey()
        {
            ServiceAccountId = serviceAccountId,
            Name = Name,
            Key = Key,
            ExpireAt = ExpireAt,
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = EncryptedPayload,
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpireAt != null && ExpireAt <= DateTime.UtcNow)
        {
            yield return new ValidationResult(
               $"Please select an expiration date that is in the future.");
        }
    }
}
