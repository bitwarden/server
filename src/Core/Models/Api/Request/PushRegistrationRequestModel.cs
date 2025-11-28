// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushRegistrationRequestModel
{
    [Required] public string DeviceId { get; set; }
    [Required] public string PushToken { get; set; }
    [Required] public string UserId { get; set; }
    [Required] public DeviceType Type { get; set; }
    [Required] public string Identifier { get; set; }
    public IEnumerable<string> OrganizationIds { get; set; }
    public Guid InstallationId { get; set; }
}
