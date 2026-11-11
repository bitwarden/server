// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Providers.Models.Request;

public class ProviderVerifyDeleteRecoverRequestModel
{
    [Required]
    public string Token { get; set; }
}
