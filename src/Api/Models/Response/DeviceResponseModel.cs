using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class DeviceResponseModel : ResponseModel
{
    public DeviceResponseModel(Device device)
        : base("device")
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        Id = device.Id.ToString();
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        CreationDate = device.CreationDate;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
}
