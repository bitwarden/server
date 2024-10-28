using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushDeviceRequestModel
{
    [Required]
    public string Id { get; set; }
}
