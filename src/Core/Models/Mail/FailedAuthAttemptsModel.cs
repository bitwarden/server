namespace Bit.Core.Models.Mail;

public class FailedAuthAttemptsModel : NewDeviceLoggedInModel
{
    public string AffectedEmail { get; set; }
}
