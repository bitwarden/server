using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Organizations;

public class OrganizationIdpRequestModel
{
    [Required]
    [StringLength(50)]
    public string IdpHost { get; set; }
}
