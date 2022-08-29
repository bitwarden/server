using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api;

public class PushUpdateRequestModel
{
    public PushUpdateRequestModel()
    { }

    public PushUpdateRequestModel(IEnumerable<string> deviceIds, string organizationId)
    {
        DeviceIds = deviceIds;
        OrganizationId = organizationId;
    }

    [Required]
    public IEnumerable<string> DeviceIds { get; set; }
    [Required]
    public string OrganizationId { get; set; }
}
