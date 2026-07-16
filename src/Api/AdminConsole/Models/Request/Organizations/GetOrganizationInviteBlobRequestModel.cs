using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class GetOrganizationInviteBlobRequestModel
{
    [Required]
    public required Guid Code { get; set; }

    [Required]
    public required Guid OrganizationId { get; set; }
}
