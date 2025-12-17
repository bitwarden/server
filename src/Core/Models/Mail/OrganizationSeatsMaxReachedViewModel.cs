// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class OrganizationSeatsMaxReachedViewModel : BaseMailModel
{
    public int MaxSeatCount { get; set; }
    public string VaultSubscriptionUrl { get; set; }
}
