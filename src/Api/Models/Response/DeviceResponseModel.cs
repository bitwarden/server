// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

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

        Id = device.Id;
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        CreationDate = device.CreationDate;
        IsTrusted = device.IsTrusted();
        EncryptedUserKey = device.EncryptedUserKey;
        EncryptedPublicKey = device.EncryptedPublicKey;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
    public bool IsTrusted { get; set; }
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }
}
