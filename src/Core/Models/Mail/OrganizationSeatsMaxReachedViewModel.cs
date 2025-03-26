namespace Bit.Core.Models.Mail;

public class OrganizationSeatsMaxReachedViewModel : BaseMailModel
{
    public int MaxSeatCount { get; set; }
    public string VaultSubscriptionUrl { get; set; }
}
