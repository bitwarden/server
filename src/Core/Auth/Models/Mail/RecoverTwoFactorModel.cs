using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class RecoverTwoFactorModel : BaseMailModel
{
    public string TheDate { get; set; }
    public string TheTime { get; set; }
    public string TimeZone { get; set; }
    public string IpAddress { get; set; }
}
