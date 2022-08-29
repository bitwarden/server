namespace Bit.Core.Models.Mail;

public class NewDeviceLoggedInModel : BaseMailModel
{
    public string TheDate { get; set; }
    public string TheTime { get; set; }
    public string TimeZone { get; set; }
    public string IpAddress { get; set; }
    public string DeviceType { get; set; }
}
