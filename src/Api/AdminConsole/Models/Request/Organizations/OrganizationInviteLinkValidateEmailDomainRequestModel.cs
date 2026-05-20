using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationInviteLinkValidateEmailDomainRequestModel
{
    [Required]
    public required Guid Code { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}
