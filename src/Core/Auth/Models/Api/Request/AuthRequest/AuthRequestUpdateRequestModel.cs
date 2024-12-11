using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.AuthRequest;

public class AuthRequestUpdateRequestModel
{
    public string Key { get; set; }
    public string MasterPasswordHash { get; set; }

    [Required]
    public string DeviceIdentifier { get; set; }

    [Required]
    public bool RequestApproved { get; set; }
}
