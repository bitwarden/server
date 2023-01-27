using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSsoDomainDetailsRequestModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
