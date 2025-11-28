using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Organizations;

public class SecretsManagerSubscribeRequestModel
{
    [Required]
    [Range(0, int.MaxValue)]
    public int AdditionalSmSeats { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int AdditionalServiceAccounts { get; set; }
}
