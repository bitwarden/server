using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class RefreshOrganizationInviteLinkRequestModel
{
    /// <summary>
    /// An opaque cryptographic blob. The server only stores and transports it, so its format is not
    /// validated here.
    /// </summary>
    [Required]
    public required string Invite { get; set; }

    public RefreshOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Invite = Invite,
    };
}
