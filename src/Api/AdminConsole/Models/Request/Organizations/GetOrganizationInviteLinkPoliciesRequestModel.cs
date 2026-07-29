using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class GetOrganizationInviteLinkPoliciesRequestModel
{
    [Required]
    public required Guid OrganizationId { get; set; }

    [Required]
    public required Guid Code { get; set; }
}
