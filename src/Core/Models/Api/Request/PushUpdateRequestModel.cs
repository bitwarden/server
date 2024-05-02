using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushUpdateRequestModel
{
    public PushUpdateRequestModel()
    { }

    public PushUpdateRequestModel(IEnumerable<KeyValuePair<string, DeviceType>> devices, string organizationId)
    {
        Devices = devices.Select(d => new PushDeviceRequestModel { Id = d.Key, Type = d.Value });
        OrganizationId = organizationId;
    }

    [Required]
    public IEnumerable<PushDeviceRequestModel> Devices { get; set; }
    [Required]
    public string OrganizationId { get; set; }
}
