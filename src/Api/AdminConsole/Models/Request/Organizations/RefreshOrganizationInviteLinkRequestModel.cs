using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class RefreshOrganizationInviteLinkRequestModel
{
    /// <summary>
    /// The invite key encrypted with the organization key.
    /// </summary>
    [Required]
    [EncryptedString]
    public required string EncryptedInviteKey { get; set; }

    /// <summary>
    /// The organization key encrypted for the invite link. Currently unused; will be populated in a future stage.
    /// </summary>
    [EncryptedString]
    public string? EncryptedOrgKey { get; set; }

    public RefreshOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        EncryptedInviteKey = EncryptedInviteKey,
        EncryptedOrgKey = EncryptedOrgKey,
    };
}
