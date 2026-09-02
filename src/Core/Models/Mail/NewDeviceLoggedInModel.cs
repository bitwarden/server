// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class NewDeviceLoggedInModel : BaseMailModel
{
    public string TheDate { get; set; }
    public string TheTime { get; set; }
    public string TimeZone { get; set; }
    public string IpAddress { get; set; }
    public string DeviceType { get; set; }
}
