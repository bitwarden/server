using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class SetupBusinessUnitRequestBody
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    public string ProviderKey { get; set; }

    [Required]
    public string OrganizationKey { get; set; }
}
