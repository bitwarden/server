using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request;

public class DeviceEncryptedKeyRequestModel
{
    [Required]
    [EncryptedString]
    public string EncryptedKey { get; set; }
}
