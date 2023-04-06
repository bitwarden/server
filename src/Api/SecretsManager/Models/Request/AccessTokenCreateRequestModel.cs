using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessTokenCreateRequestModel
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

    public AccessCheck ToAccessCheck(Guid id, Guid organizationId, Guid userId)
    {
        return new AccessCheck
        {
            AccessOperationType = AccessOperationType.CreateAccessToken,
            TargetId = id,
            OrganizationId = organizationId,
            UserId = userId,
        };
    }
}
