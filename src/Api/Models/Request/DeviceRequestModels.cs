using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request;

public class DeviceRequestModel
{
    [Required]
    public DeviceType? Type { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string Identifier { get; set; }

    [StringLength(255)]
    public string PushToken { get; set; }

    public Device ToDevice(Guid? userId = null)
    {
        return ToDevice(new Device { UserId = userId == null ? default(Guid) : userId.Value });
    }

    public Device ToDevice(Device existingDevice)
    {
        existingDevice.Name = Name;
        existingDevice.Identifier = Identifier;
        existingDevice.PushToken = PushToken;
        existingDevice.Type = Type.Value;

        return existingDevice;
    }
}

public class DeviceTokenRequestModel
{
    [StringLength(255)]
    public string PushToken { get; set; }

    public Device ToDevice(Device existingDevice)
    {
        existingDevice.PushToken = PushToken;
        return existingDevice;
    }
}

public class DeviceKeysRequestModel
{
    /// <inheritdoc cref="Device.EncryptedUserKey" />
    [Required]
    [EncryptedString]
    public string EncryptedUserKey { get; set; }

    /// <inheritdoc cref="Device.EncryptedPublicKey" />
    [Required]
    [EncryptedString]
    public string EncryptedPublicKey { get; set; }

    /// <inheritdoc cref="Device.EncryptedPrivateKey" />
    [Required]
    [EncryptedString]
    public string EncryptedPrivateKey { get; set; }

    public Device ToDevice(Device existingDevice)
    {
        existingDevice.EncryptedUserKey = EncryptedUserKey;
        existingDevice.EncryptedPublicKey = EncryptedPublicKey;
        existingDevice.EncryptedPrivateKey = EncryptedPrivateKey;

        return existingDevice;
    }
}
