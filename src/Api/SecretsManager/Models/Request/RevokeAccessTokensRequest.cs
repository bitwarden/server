using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Models.Data;

public class RevokeAccessTokensRequest
{
    [Required]
    public Guid[] Ids { get; set; }

    public AccessCheck ToAccessCheck(Guid targetId, Guid organizationId, Guid userId)
    {
        return new AccessCheck
        {
            OperationType = OperationType.RevokeAccessToken,
            TargetId = targetId,
            OrganizationId = organizationId,
            UserId = userId,
        };
    }
}
