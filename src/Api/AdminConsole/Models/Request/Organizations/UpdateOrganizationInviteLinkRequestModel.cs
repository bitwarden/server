using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class UpdateOrganizationInviteLinkRequestModel
{
    /// <summary>
    /// Email domains permitted to accept the invite link (e.g. <c>["acme.com"]</c>).
    /// </summary>
    [Required]
    [MinLength(1)]
    [ValidateSequence<DomainNameValidatorAttribute>]
    public required IEnumerable<string> AllowedDomains { get; set; }

    public UpdateOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        AllowedDomains = AllowedDomains,
    };
}
