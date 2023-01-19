using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Api.Models.Request;

public class DeviceVerificationRequestModel
{
    [Obsolete("Leaving this for backwards compatibilty on clients")]
    [Required]
    public bool UnknownDeviceVerificationEnabled { get; set; }
}
