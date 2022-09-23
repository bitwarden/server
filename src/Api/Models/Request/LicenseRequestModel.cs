using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request;

public class LicenseRequestModel
{
    [Required]
    public IFormFile License { get; set; }
}
