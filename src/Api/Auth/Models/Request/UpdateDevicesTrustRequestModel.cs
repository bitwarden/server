using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Auth.Models.Api.Request;

#nullable enable

namespace Bit.Api.Auth.Models.Request;

public class UpdateDevicesTrustRequestModel : SecretVerificationRequestModel
{
    [Required]
    public DeviceKeysUpdateRequestModel CurrentDevice { get; set; } = null!;
    public IEnumerable<OtherDeviceKeysUpdateRequestModel>? OtherDevices { get; set; }
}
