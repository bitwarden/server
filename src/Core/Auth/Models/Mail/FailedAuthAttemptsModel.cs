using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class FailedAuthAttemptsModel : NewDeviceLoggedInModel
{
    public string AffectedEmail { get; set; }
}
