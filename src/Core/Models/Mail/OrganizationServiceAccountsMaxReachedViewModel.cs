// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class OrganizationServiceAccountsMaxReachedViewModel : BaseMailModel
{
    public int MaxServiceAccountsCount { get; set; }
    public string VaultSubscriptionUrl { get; set; }
    public string OrganizationName { get; set; }
}
