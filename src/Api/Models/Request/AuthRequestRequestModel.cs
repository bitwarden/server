using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Api.Models.Request;

public class AuthRequestCreateRequestModel
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string PublicKey { get; set; }
    [Required]
    public string DeviceIdentifier { get; set; }
    [Required]
    [StringLength(25)]
    public string AccessCode { get; set; }
    [Required]
    public AuthRequestType? Type { get; set; }
    [Required]
    public string FingerprintPhrase { get; set; }
}

public class AuthRequestUpdateRequestModel
{
    public string Key { get; set; }
    public string MasterPasswordHash { get; set; }
    [Required]
    public string DeviceIdentifier { get; set; }
    [Required]
    public bool RequestApproved { get; set; }
}
