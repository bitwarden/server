// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class ClaimedDomainUserNotificationViewModel : BaseTitleContactUsMailModel
{
    public string OrganizationName { get; init; }
    public string DomainName { get; init; }
    public string EmailDomain { get; init; }
    public string UserEmail { get; init; }
}
