using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class RefreshOrganizationInviteLinkRequestModel
{
    /// <summary>
    /// An opaque cryptographic blob. The server only stores and transports it, so its format is not
    /// validated here.
    /// </summary>
    [Required]
    [EncryptedStringLength(3000)]
    public required string Invite { get; set; }

    /// <summary>
    /// Indicates if the link supports user auto confirmation (not supported yet).
    /// </summary>
    [Required]
    public required bool SupportsConfirmation { get; set; }

    public RefreshOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Invite = Invite,
        SupportsConfirmation = SupportsConfirmation,
    };
}
