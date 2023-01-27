using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationDomainSsoDetailsRequestModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
