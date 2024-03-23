using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushDeviceRequestModel
{
    [Required]
    public string Id { get; set; }
    [Required]
    public DeviceType Type { get; set; }
}
