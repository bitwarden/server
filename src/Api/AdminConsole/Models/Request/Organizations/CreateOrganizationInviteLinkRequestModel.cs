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

    public CreateOrganizationInviteLinkRequest ToCommandRequest(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        AllowedDomains = AllowedDomains,
        EncryptedInviteKey = EncryptedInviteKey,
        EncryptedOrgKey = EncryptedOrgKey,
    };
}
