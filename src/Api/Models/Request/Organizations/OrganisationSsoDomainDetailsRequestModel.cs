using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class OrganisationSsoDomainDetailsRequestModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
