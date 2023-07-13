using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Auth.Models.Api.Response;

public class ProtectedDeviceResponseModel : ResponseModel
{
    public ProtectedDeviceResponseModel(Device device)
        : base("protectedDevice")
    {
        ArgumentNullException.ThrowIfNull(device);

        Id = device.Id;
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        CreationDate = device.CreationDate;
        EncryptedUserKey = device.EncryptedUserKey;
        EncryptedPublicKey = device.EncryptedPublicKey;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
    public string EncryptedUserKey { get; set; }
    public string EncryptedPublicKey { get; set; }
}
