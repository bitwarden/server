using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Enums;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Request;

public class RevokeAccessTokensRequest
{
    [Required]
    public Guid[] Ids { get; set; }

    public AccessCheck ToAccessCheck(Guid targetId, Guid organizationId, Guid userId)
    {
        return new AccessCheck
        {
            AccessOperationType = AccessOperationType.RevokeAccessToken,
            TargetId = targetId,
            OrganizationId = organizationId,
            UserId = userId,
        };
    }
}
