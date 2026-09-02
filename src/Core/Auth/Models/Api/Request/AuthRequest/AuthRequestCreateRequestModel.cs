// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Enums;

namespace Bit.Core.Auth.Models.Api.Request.AuthRequest;

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
}
