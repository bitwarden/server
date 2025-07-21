// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class EmergencyAccessRecoveryViewModel : BaseMailModel
{
    public string Name { get; set; }
    public string Action { get; set; }
    public int DaysLeft { get; set; }
}
