using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Organizations;

public class OrganizationIdpRequestModel
{
    [Required]
    [StringLength(100)]
    public string IdpHost { get; set; }
}
