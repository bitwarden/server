// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationDomainSsoDetailsRequestModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
