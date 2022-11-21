using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

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
    [Required]
    public DateTime ExpireAt { get; set; }

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
}
