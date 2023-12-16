using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Models.Request;

public class KnownDeviceRequestModel
{
    [Required]
    [FromHeader(Name = "X-Request-Email")]
    public string Email { get; set; }

    [Required]
    [FromHeader(Name = "X-Device-Identifier")]
    public string DeviceIdentifier { get; set; }
    
}