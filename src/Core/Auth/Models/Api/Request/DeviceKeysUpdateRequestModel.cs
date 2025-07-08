// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request;

public class OtherDeviceKeysUpdateRequestModel : DeviceKeysUpdateRequestModel
{
    [Required]
    public Guid DeviceId { get; set; }

    public Device ToDevice(Device existingDevice)
    {
        existingDevice.EncryptedPublicKey = EncryptedPublicKey;
        existingDevice.EncryptedUserKey = EncryptedUserKey;
        return existingDevice;
    }
}

public class DeviceKeysUpdateRequestModel
{
    [Required]
    [EncryptedString]
    public string EncryptedPublicKey { get; set; }

    [Required]
    [EncryptedString]
    public string EncryptedUserKey { get; set; }
}
