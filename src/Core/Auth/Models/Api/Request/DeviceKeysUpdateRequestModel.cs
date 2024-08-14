using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request;

public class OtherDeviceKeysUpdateRequestModel : DeviceKeysUpdateRequestModel
{
    [Required]
    public Guid DeviceId { get; set; }
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
