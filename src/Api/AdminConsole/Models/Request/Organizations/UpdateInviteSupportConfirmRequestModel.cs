using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class UpdateInviteSupportConfirmRequestModel
{
    /// <summary>
    /// An opaque cryptographic blob. The server only stores and transports it, so its format is not
    /// validated here.
    /// </summary>
    [Required]
    [EncryptedStringLength(3000)]
    public required string Invite { get; set; }

    /// <summary>
    /// Whether this invite link can be used to confirm a user.
    /// </summary>
    [Required]
    public required bool SupportsConfirmation { get; set; }

    public UpdateInviteSupportConfirmRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Invite = Invite,
        SupportsConfirmation = SupportsConfirmation,
    };
}
