namespace Bit.Core.Models.Mail;

public class OrganizationSeatsAutoscaledViewModel : BaseMailModel
{
    public int InitialSeatCount { get; set; }
    public int CurrentSeatCount { get; set; }
    public string VaultSubscriptionUrl { get; set; }
}
