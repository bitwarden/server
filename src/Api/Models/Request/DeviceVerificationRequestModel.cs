using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;

namespace Bit.Api.Models.Request;

public class DeviceVerificationRequestModel
{
    [Required]
    public bool UnknownDeviceVerificationEnabled { get; set; }

    public User ToUser(User user)
    {
        user.UnknownDeviceVerificationEnabled = UnknownDeviceVerificationEnabled;
        return user;
    }
}
