using System.ComponentModel.DataAnnotations;

#nullable enable

namespace Bit.Api.Auth.Models.Request;

public class UntrustDevicesRequestModel
{
    [Required]
    public IEnumerable<Guid> Devices { get; set; } = null!;
}
