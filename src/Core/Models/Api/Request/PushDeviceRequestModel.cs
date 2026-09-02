// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushDeviceRequestModel
{
    [Required]
    public string Id { get; set; }
}
