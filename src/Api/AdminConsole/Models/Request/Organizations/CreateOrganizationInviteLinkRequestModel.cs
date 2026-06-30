using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class CreateOrganizationInviteLinkRequestModel
{
    /// <summary>
    /// Email domains permitted to accept the invite link (e.g. <c>["acme.com"]</c>).
    /// </summary>
    [Required]
    [MinLength(1)]
    [ValidateSequence<DomainNameValidatorAttribute>]
    public required IEnumerable<string> AllowedDomains { get; set; }

    /// <summary>
    /// An opaque cryptographic blob. The server only stores and transports it, so its format is not
    /// validated here.
    /// </summary>
    [Required]
    public required string Invite { get; set; }

    public CreateOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        AllowedDomains = AllowedDomains,
        Invite = Invite,
    };
}
