using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushDeviceRequestModel
{
    [Required]
    public string Id { get; set; }
    [DefaultValue(ApplicationChannel.PasswordManagerProduction)]
    public ApplicationChannel Channel { get; set; }
}
