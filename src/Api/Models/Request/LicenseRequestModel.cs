// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request;

public class LicenseRequestModel
{
    [Required]
    public IFormFile License { get; set; }
}
