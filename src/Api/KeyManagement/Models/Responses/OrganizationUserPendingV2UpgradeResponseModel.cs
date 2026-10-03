using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.KeyManagement.Models.Data;
using Bit.HttpExtensions;

namespace Bit.Api.KeyManagement.Models.Responses;

/// <summary>
/// A membership whose account recovery key still wraps the member's V1 user key.
/// </summary>
/// <remarks>
/// The organization's private key is not included. Read it from <c>GET organizations/{orgId}/private-key</c>.
/// Callers of this endpoint always hold the ManageUsers permission that route requires.
/// </remarks>
public class OrganizationUserPendingV2UpgradeResponseModel : ResponseModel
{
    [SetsRequiredMembers]
    public OrganizationUserPendingV2UpgradeResponseModel(OrganizationUserV2UpgradeDetails details,
        V2UpgradeTokenData token)
        : base("organizationUserPendingV2Upgrade")
    {
        OrganizationUserId = details.OrganizationUserId;
        UserKeyId = details.UserKeyId;
        AccountRecoveryKey = details.AccountRecoveryKey;
        V2UpgradeToken = new V2UpgradeTokenResponseModel
        {
            WrappedUserKey1 = token.WrappedUserKey1,
            WrappedUserKey2 = token.WrappedUserKey2
        };
    }

    public required Guid OrganizationUserId { get; init; }

    /// <summary>
    /// The key id of the member's current user key. Return it unchanged with the re-wrapped key.
    /// </summary>
    [Required]
    public required string UserKeyId { get; init; }

    /// <summary>
    /// The member's V1 user key wrapped with the organization's public key.
    /// </summary>
    [Required]
    public required string AccountRecoveryKey { get; init; }

    /// <summary>
    /// Contains the V2 user key wrapped with the V1 user key, in <c>WrappedUserKey2</c>.
    /// </summary>
    public required V2UpgradeTokenResponseModel V2UpgradeToken { get; init; }
}
