// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushUpdateRequestModel
{
    public PushUpdateRequestModel()
    { }

    public PushUpdateRequestModel(IEnumerable<string> deviceIds, string organizationId)
    {
        Devices = deviceIds.Select(d => new PushDeviceRequestModel { Id = d });
        OrganizationId = organizationId;
    }

    [Required]
    public IEnumerable<PushDeviceRequestModel> Devices { get; set; }
    [Required]
    public string OrganizationId { get; set; }
}
