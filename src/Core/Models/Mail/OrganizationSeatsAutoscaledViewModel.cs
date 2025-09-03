// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class OrganizationSeatsAutoscaledViewModel : BaseMailModel
{
    public int InitialSeatCount { get; set; }
    public int CurrentSeatCount { get; set; }
    public string VaultSubscriptionUrl { get; set; }
}
