using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationVerifyDeleteRecoverRequestModel
{
    [Required]
    public string Token { get; set; }
}
