using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.Vault.Models.Request;

public class CollectionApiKeyCreateRequestModel : IValidatableObject
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(200)]
    public required string Name { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(4000)]
    public required string EncryptedPayload { get; set; }

    [Required]
    [EncryptedString]
    public required string Key { get; set; }

    public DateTime? ExpireAt { get; set; }

    public ApiKey ToApiKey(Guid organizationId, Guid collectionId)
    {
        return new ApiKey()
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            Name = Name,
            Key = Key,
            ExpireAt = ExpireAt,
            Scope = "[\"api.vault\"]",
            EncryptedPayload = EncryptedPayload,
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpireAt != null && ExpireAt <= DateTime.UtcNow)
        {
            yield return new ValidationResult(
                "Please select an expiration date that is in the future.");
        }
    }
}
