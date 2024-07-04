using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushUpdateRequestModel
{
    public PushUpdateRequestModel()
    { }

    public PushUpdateRequestModel(IEnumerable<(string deviceIds, ApplicationChannel channel)> devices, string organizationId)
    {
        Devices = devices.Select(d => new PushDeviceRequestModel { Id = d.deviceIds, Channel = d.channel });
        OrganizationId = organizationId;
    }

    [Required]
    public IEnumerable<PushDeviceRequestModel> Devices { get; set; }
    [Required]
    public string OrganizationId { get; set; }
}
