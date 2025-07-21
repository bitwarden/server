// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class EmergencyAccessAcceptedViewModel : BaseMailModel
{
    public string GranteeEmail { get; set; }
}
