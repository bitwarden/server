using System.ComponentModel.DataAnnotations;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderVerifyDeleteRecoverRequestModel
{
    [Required]
    public string Token { get; set; }
}
