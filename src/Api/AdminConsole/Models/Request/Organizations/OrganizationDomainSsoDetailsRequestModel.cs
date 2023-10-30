using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationDomainSsoDetailsRequestModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
